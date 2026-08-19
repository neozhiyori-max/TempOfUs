from pathlib import Path
import dnfile

assembly_path = Path('/home/ubuntu/tempMOD/game_refs/Assembly-CSharp.dll')
needles = ('MainMenuManager', 'PlayerControl', 'GameData', 'MeetingHud', 'HudManager', 'DeadBody', 'ShipStatus')

pe = dnfile.dnPE(str(assembly_path))
for type_def in pe.net.mdtables.TypeDef:
    namespace = str(type_def.TypeNamespace or '')
    name = str(type_def.TypeName or '')
    full_name = f'{namespace}.{name}'.strip('.')
    if not any(needle.lower() in full_name.lower() for needle in needles):
        continue

    print(f'## {full_name}')
    for method_index in type_def.MethodList:
        method = method_index.row
        method_name = str(method.Name or '')
        if any(part.lower() in method_name.lower() for part in ('start', 'awake', 'update', 'murder', 'kill', 'die', 'report', 'meeting', 'vote', 'rpc')):
            print(f'  - {method_name}')
