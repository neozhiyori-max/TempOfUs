from pathlib import Path
import sys
import dnfile

if len(sys.argv) != 3:
    raise SystemExit('usage: tools_inspect_assembly_type.py <assembly-path> <TypeName>')

assembly_path = Path(sys.argv[1])
wanted = sys.argv[2]
pe = dnfile.dnPE(str(assembly_path))
for type_def in pe.net.mdtables.TypeDef:
    name = str(type_def.TypeName or '')
    namespace = str(type_def.TypeNamespace or '')
    if name != wanted:
        continue
    print(f'## {namespace}.{name}'.strip('.'))
    print('FIELDS')
    for item in type_def.FieldList:
        print(f'  {item.row.Name}')
    print('METHODS')
    for item in type_def.MethodList:
        print(f'  {item.row.Name}')
    break
else:
    raise SystemExit(f'type not found: {wanted}')
