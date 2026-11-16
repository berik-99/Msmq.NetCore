"""Regenerate external IntelliSense XML from a pinned Microsoft documentation snapshot."""

from concurrent.futures import ThreadPoolExecutor
from copy import deepcopy
from pathlib import Path
import re
from urllib.request import Request, urlopen
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
COMMIT = "d2f51ac472178114356e2a26a36ec366e35f8b7c"
UPSTREAM = f"https://raw.githubusercontent.com/dotnet/dotnet-api-docs/{COMMIT}"
OLD_NAMESPACE = "System.Messaging."
NEW_NAMESPACE = "Msmq.NetCore.Messaging."
DOC_TAGS = {"summary", "param", "typeparam", "returns", "value", "exception", "seealso"}


def download(path):
    request = Request(f"{UPSTREAM}/{path}", headers={"User-Agent": "Msmq.NetCore-docs"})
    with urlopen(request, timeout=60) as response:
        return response.read()


def public_types():
    names = set()
    for path in (ROOT / "Sources/Messaging").glob("*.cs"):
        source = path.read_text(encoding="utf-8-sig")
        names.update(re.findall(
            r"(?m)^public\s+(?:(?:sealed|abstract|static)\s+)*(?:class|enum|interface)\s+(\w+)",
            source))
        names.update(re.findall(r"(?m)^public\s+delegate\s+\w+\s+(\w+)\s*\(", source))
    return sorted(names)


def documentation_entries(name):
    source = ET.fromstring(download(f"xml/System.Messaging/{name}.xml"))
    entries = []
    for item in [source, *source.findall("./Members/Member")]:
        member_name = item.attrib.get("MemberName", "")
        if "." in member_name and member_name != ".ctor":
            continue  # Explicit interface implementations are not public declarations.
        if member_name == "Finalize":
            implementation = (ROOT / f"Sources/Messaging/{name}.cs").read_text(encoding="utf-8-sig")
            if not re.search(rf"~{re.escape(name)}\s*\(", implementation):
                continue  # The port uses SafeHandle instead of these legacy finalizers.
        versions = item.attrib.get("FrameworkAlternate", "")
        if versions and "netframework-4.8.1" not in versions.split(";"):
            continue
        signature_tag = "TypeSignature" if item is source else "MemberSignature"
        signature = item.find(f"{signature_tag}[@Language='DocId']")
        docs = item.find("Docs")
        if signature is None or docs is None:
            continue
        member = ET.Element("member", name=signature.attrib["Value"].replace(OLD_NAMESPACE, NEW_NAMESPACE))
        for child in docs:
            if child.tag not in DOC_TAGS:
                continue
            versions = child.attrib.get("FrameworkAlternate", "")
            if versions and "netframework-4.8.1" not in versions.split(";"):
                continue
            if child.tag == "exception" and child.attrib.get("cref") == "T:System.Security.SecurityException":
                continue  # CAS permission demands are not enforced by .NET Core.
            node = deepcopy(child)
            for descendant in node.iter():
                descendant.attrib.pop("FrameworkAlternate", None)
            node = ET.fromstring(ET.tostring(node, encoding="unicode").replace(OLD_NAMESPACE, NEW_NAMESPACE))
            member.append(node)
        if member.find("summary") is None:
            raise ValueError(f"No summary for {member.attrib['name']}")
        entries.append(member)
    return entries


def add_note(member, text):
    remarks = member.find("remarks")
    if remarks is None:
        remarks = ET.SubElement(member, "remarks")
    ET.SubElement(remarks, "para").text = text


def main():
    with ThreadPoolExecutor(max_workers=8) as pool:
        batches = list(pool.map(documentation_entries, public_types()))
    entries = [entry for batch in batches for entry in batch]
    by_id = {entry.attrib["name"]: entry for entry in entries}
    if len(by_id) != len(entries):
        raise ValueError("Duplicate documentation member IDs")

    for member in entries:
        for node in member.iter():
            cref = node.attrib.get("cref", "")
            if "MessageQueueInstaller" in cref or ".Messaging.Design." in cref:
                node.attrib.pop("cref")
                original = cref[2:].replace(NEW_NAMESPACE, OLD_NAMESPACE).lower()
                node.set("href", f"https://learn.microsoft.com/dotnet/api/{original}")

    notes = {
        "T:Msmq.NetCore.Messaging.MessageQueue":
            "Windows and an installed MSMQ service are required. This port targets .NET Core and modern .NET. "
            "Use the official System.Messaging assembly on .NET Framework. MessageQueueInstaller and the "
            "Windows Forms queue-path designer are not provided. Distributed transactions depend on the runtime and MSDTC.",
        "T:Msmq.NetCore.Messaging.BinaryMessageFormatter":
            "Legacy compatibility only. BinaryFormatter is obsolete and unsafe for untrusted messages. "
            "On .NET 9 and later, the executable must explicitly reference the unsupported "
            "System.Runtime.Serialization.Formatters compatibility package and enable unsafe binary serialization. "
            "The library does not enable it automatically.",
        "T:Msmq.NetCore.Messaging.MessageQueuePermission":
            "Retained for API compatibility. Code Access Security is not enforced by .NET Core or modern .NET. "
            "Windows queue ACLs enforce access.",
        "T:Msmq.NetCore.Messaging.MessageQueuePermissionAttribute":
            "Retained for API compatibility. This attribute does not enforce Code Access Security on .NET Core or modern .NET.",
        "M:Msmq.NetCore.Messaging.ActiveXMessageFormatter.Read(Msmq.NetCore.Messaging.Message)":
            "Compatibility difference: VT_I1 is returned as System.SByte. System.Messaging returns System.Byte "
            "for this variant; FF therefore becomes -1 in this port rather than 255. Truncated primitive bodies "
            "throw System.IO.EndOfStreamException.",
        "M:Msmq.NetCore.Messaging.ActiveXMessageFormatter.Write(Msmq.NetCore.Messaging.Message,System.Object)":
            "The serialized body is preserved when this method is called directly before MessageQueue.Send, "
            "even when the message has no Formatter or previously contained an XML body.",
        "P:Msmq.NetCore.Messaging.XmlMessageFormatter.TargetTypes":
            "Compatibility difference: replacing this array clears previously cached serializers. "
            "The serializer table is rebuilt from the current TargetTypes and TargetTypeNames on the next read.",
        "P:Msmq.NetCore.Messaging.XmlMessageFormatter.TargetTypeNames":
            "Compatibility difference: replacing this array clears previously cached serializers. "
            "The serializer table is rebuilt from the current TargetTypes and TargetTypeNames on the next read.",
    }
    for member_id, text in notes.items():
        add_note(by_id[member_id], text)

    root = ET.Element("doc")
    assembly = ET.SubElement(root, "assembly")
    ET.SubElement(assembly, "name").text = "Msmq.NetCore"
    members = ET.SubElement(root, "members")
    members.extend(sorted(entries, key=lambda member: member.attrib["name"]))
    ET.indent(root, space="  ")
    output = ROOT / "Sources/Documentation"
    license_text = download("LICENSE")
    output.mkdir(parents=True, exist_ok=True)
    header = (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<!-- Adapted from Microsoft .NET API documentation. Copyright Microsoft and contributors.\n'
        f'     Source: https://github.com/dotnet/dotnet-api-docs/tree/{COMMIT}/xml/System.Messaging\n'
        '     Licensed under CC BY 4.0: https://creativecommons.org/licenses/by/4.0/\n'
        '     Changes: namespace/member mapping, IntelliSense extraction, CAS and compatibility notes.\n'
        '     See ThirdPartyNotices.txt and LICENSE-API-DOCS.txt in the package. -->\n'
    )
    (output / "Msmq.NetCore.xml").write_text(header + ET.tostring(root, encoding="unicode") + "\n", encoding="utf-8")
    (output / "LICENSE-API-DOCS.txt").write_bytes(license_text)
    print(f"Generated {len(entries)} documentation entries for {len(public_types())} public types.")


if __name__ == "__main__":
    main()
