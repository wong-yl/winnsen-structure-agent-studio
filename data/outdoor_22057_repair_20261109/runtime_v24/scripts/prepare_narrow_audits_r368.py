from generate_model import STAGE,sha,write

for source,destination in [('audit_layout_r345.py','audit_layout_r368.py'),('audit_shade_r239.py','audit_shade_r369.py')]:
    p=STAGE/'scripts'/destination;assert not p.exists()
    text=(STAGE/'scripts'/source).read_text(encoding='utf-8')
    assert '129 + 19 * count' in text or "129+19*spec['doors']" in text
    text=text.replace('129 + 19 * count','109 + 19 * count').replace("129+19*spec['doors']","109+19*spec['doors']")
    if destination=='audit_shade_r369.py':
        old="columns|{'太阳能板压条.SLDPRT','塑料灯罩.SLDPRT','塑料板压条焊件.SLDPRT'}"
        assert old in text
        text=text.replace(old,"columns|{'太阳能板压条.SLDPRT'}")
    p.write_text(text,encoding='utf-8')
generator=STAGE/'scripts/generate_layout_model.py';text=generator.read_text(encoding='utf-8')
text=text.replace("return (129 + 19 * spec['doors']", "return ((109 if spec['width'] < 900 else 129) + 19 * spec['doors']")
text=text.replace("shade_audit = 'audit_shade_r201.py' if legacy_pilot else 'audit_shade_r239.py'", "shade_audit = 'audit_shade_r369.py' if spec['width'] < 900 else 'audit_shade_r201.py' if legacy_pilot else 'audit_shade_r239.py'")
text=text.replace("'audit_layout_r345.py' if spec['width'] < 900", "'audit_layout_r368.py' if spec['width'] < 900")
text=text.replace("('audit_narrow_auxiliary_r346.py', 'auxiliary')", "('audit_narrow_auxiliary_r346.py', 'auxiliary'), ('audit_narrow_lamp_r370.py', 'lamp')")
text=text.replace("('layout', 'fasteners', 'auxiliary')", "('layout', 'fasteners', 'auxiliary', 'lamp')")
generator.write_text(text,encoding='utf-8')
write(STAGE/'evidence/narrow_audit_r368_build.json',{'status':'PREPARED_NOT_ACCEPTED','preserved_fixed_audit':'audit_shade_r239.py','changes':'Narrow body accounting subtracts the removed 20 surplus LED stock bodies; only approved plastic cover and retainer leave the fixed-geometry list, with separate dimensional and interface checks','files':{str(p):sha(p) for p in [STAGE/'scripts/audit_layout_r368.py',STAGE/'scripts/audit_shade_r369.py',generator]}})
