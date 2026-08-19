from pathlib import Path
import sys
import dnfile

if len(sys.argv) != 3:
    raise SystemExit('usage: tools_inspect_field_signature.py <TypeName> <FieldName>')

wanted_type, wanted_field = sys.argv[1:]
assembly = Path('/home/ubuntu/tempMOD/game_refs/Assembly-CSharp.dll')
pe = dnfile.dnPE(str(assembly))
for type_def in pe.net.mdtables.TypeDef:
    if str(type_def.TypeName or '') != wanted_type:
        continue
    for field in type_def.FieldList:
        if str(field.row.Name or '') == wanted_field:
            print(field.row)
            print('signature:', field.row.Signature)
            raise SystemExit(0)
raise SystemExit(f'not found: {wanted_type}.{wanted_field}')
