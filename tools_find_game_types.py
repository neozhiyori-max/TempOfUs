from pathlib import Path
import sys
import dnfile

if len(sys.argv) != 2:
    raise SystemExit('usage: tools_find_game_types.py <case-insensitive-keyword>')

keyword = sys.argv[1].lower()
assembly = Path('/home/ubuntu/tempMOD/game_refs/Assembly-CSharp.dll')
pe = dnfile.dnPE(str(assembly))
for type_def in pe.net.mdtables.TypeDef:
    name = str(type_def.TypeName or '')
    namespace = str(type_def.TypeNamespace or '')
    full_name = f'{namespace}.{name}'.strip('.')
    if keyword in full_name.lower():
        print(full_name)
