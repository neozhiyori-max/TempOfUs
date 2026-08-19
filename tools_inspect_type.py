from pathlib import Path
import sys
import dnfile

if len(sys.argv) != 2:
    raise SystemExit('usage: tools_inspect_type.py <TypeName>')

wanted = sys.argv[1]
pe = dnfile.dnPE('/home/ubuntu/tempMOD/game_refs/Assembly-CSharp.dll')
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
