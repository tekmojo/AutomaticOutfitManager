"""Read-only source/XML and compiled .NET text audit. Python standard library only."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct

# Typical mojibake markers; valid Unicode punctuation is permitted.
SUSPECT = re.compile(r'\ufffd|[\u0080-\u009f]|[\u00c2\u00c3][\u0080-\u00bf]|\u00e2[\u0080-\u00ff\u02c6\u20ac]|\u00f0[\u009f\u0178]|\u00ef\u00bb\u00bf')

def user_strings(path):
    b = path.read_bytes()
    u16 = lambda p: struct.unpack_from('<H', b, p)[0]
    u32 = lambda p: struct.unpack_from('<I', b, p)[0]
    pe = u32(0x3c)
    assert b[pe:pe+4] == b'PE\0\0'
    optional = pe + 24
    sections = optional + u16(pe+20)
    def file_offset(rva):
        for i in range(u16(pe+6)):
            s = sections + i*40
            size, base, raw_size, raw = struct.unpack_from('<IIII', b, s+8)
            if base <= rva < base + max(size, raw_size):
                return raw + rva-base
        raise ValueError('RVA not in a section')
    directories = optional + (96 if u16(optional) == 0x10b else 112)
    cli = file_offset(u32(directories+14*8))
    metadata = file_offset(u32(cli+8))
    assert b[metadata:metadata+4] == b'BSJB'
    p = (metadata + 16 + u32(metadata+12) + 3) & ~3
    stream_count = u16(p+2)
    p += 4
    strings = []
    for _ in range(stream_count):
        offset, size = u32(p), u32(p+4)
        end = b.index(0, p+8)
        name = b[p+8:end].decode('ascii')
        p = (end+4) & ~3
        if name != '#US':
            continue
        q, limit = metadata+offset+1, metadata+offset+size
        while q < limit:
            token_offset = q-(metadata+offset)
            first = b[q]
            q += 1
            if first < 0x80:
                length = first
            elif first < 0xc0:
                length = ((first & 0x3f)<<8) | b[q]
                q += 1
            else:
                length = ((first & 0x1f)<<24) | (b[q]<<16) | (b[q+1]<<8) | b[q+2]
                q += 3
            if length == 0:
                continue
            value = b[q:q+length-1].decode('utf-16-le')
            strings.append({'offset': token_offset, 'text': value})
            q += length
    return strings

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--assembly', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    assembly = args.assembly or args.root/'1.6/Assemblies/AutomaticOutfitManager.dll'
    findings, count = [], 0
    for folder in ('Source', 'Defs', 'About'):
        for path in sorted((args.root/folder).rglob('*')):
            if path.suffix not in ('.cs', '.xml', '.csproj') or any(p in ('obj', 'bin') for p in path.parts):
                continue
            count += 1
            try:
                content = path.read_text(encoding='utf-8-sig')
            except UnicodeError as exc:
                findings.append({'file': str(path), 'error': str(exc)})
                continue
            findings.extend({'file': str(path), 'line': i, 'text': line}
                            for i, line in enumerate(content.splitlines(), 1) if SUSPECT.search(line))
    strings = user_strings(assembly)
    if not strings:
        raise ValueError('Missing or empty compiled user-string table')
    findings.extend({'assembly': str(assembly), **s} for s in strings if SUSPECT.search(s['text']))
    report = {'files_checked': count, 'compiled_strings_checked': len(strings),
              'assembly_sha256': hashlib.sha256(assembly.read_bytes()).hexdigest().upper(),
              'findings': findings, 'limits': 'Encoding heuristic and strict decoding; visual UI smoke is separate.'}
    if args.output:
        args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(report, ensure_ascii=True, indent=2))
    return 1 if findings else 0

if __name__ == '__main__':
    raise SystemExit(main())
