import sys
# Genera ROCOLA_PULSA y ROCOLA_CAPTURA para recorrer Ctrl+F3 con Tab y Mayus+Tab.
p = ["7:17+114"]; c = []
t = 9.0
for i in range(45):
    p.append("%.2f:9" % t); t += 0.12
c.append("%.2f:tab45" % (t + 0.3))
t += 0.6
for i in range(12):
    p.append("%.2f:16+9" % t); t += 0.12
c.append("%.2f:shifttab12" % (t + 0.3))
t += 0.6
p.append("%.2f:clic:818:53" % t); p.append("%.2f:raton:1000:620" % (t + 0.3))
t += 1.5
for i in range(25):
    p.append("%.2f:9" % t); t += 0.12
c.append("%.2f:fondo-tab25" % (t + 0.5))
print(",".join(p))
print(",".join(c))
