"""Check that expanded width bounds retain the accepted door-height contract."""
from generate_model import STAGE, read, sha, write
from layout_contract import validate_specification

spec=read(STAGE/'evidence/width600_low_r308_specification.json')
accepted=[]
for width in (600,600.001,750,899.999,900,950,1200):
    result=validate_specification(dict(spec,width=width))
    assert result['width']==width and result['doors']==2
    accepted.append(width)
rejected=[]
for width in (599.999,1200.001,600.0001,True,float('nan')):
    try: validate_specification(dict(spec,width=width))
    except (ValueError,TypeError): rejected.append(str(width))
    else: raise AssertionError(width)
high=validate_specification(read(STAGE/'evidence/width600_high_r308_specification.json'))
assert high['doors']==22
bad=dict(spec,columns=[{'side':'L','doors':[{'heightMm':169},{'heightMm':1404}]},spec['columns'][1]])
try: validate_specification(bad)
except ValueError: pass
else: raise AssertionError('Sub-170 door accepted')
write(STAGE/'evidence/width_contract_r321.json',{'status':'PASS','accepted_widths_mm':accepted,'rejected':rejected,'minimum_door_height_mm':170,'maximum_doors':22,'sources':{str(STAGE/'scripts/layout_contract.py'):sha(STAGE/'scripts/layout_contract.py')}})
print('PASS: width boundary and existing door-height contract')
