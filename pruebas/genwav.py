import numpy as np, wave
rng=np.random.default_rng(7)
FS=44100
def escribe(nombre, dur, env, brillo=None):
    n=int(dur*FS); t=np.arange(n)/FS
    e=env(t); b=np.full(n,0.8) if brillo is None else brillo(t)
    v =0.45*np.sin(2*np.pi*82.4*t)
    v+=0.30*np.sin(2*np.pi*220.0*t+0.4)
    v+=0.22*np.sin(2*np.pi*440.0*t+1.1)
    v+=0.14*b*np.sin(2*np.pi*2200.0*t+0.7)
    v+=0.10*b*np.sin(2*np.pi*6500.0*t+2.0)
    fase=np.arange(n)%int(FS*0.5)
    v+=0.55*np.exp(-fase/(FS*0.006))
    v+=0.035*b*rng.uniform(-1,1,n)
    v=v*e*0.30
    d=np.clip(v*32000,-32000,32000).astype('<i2')
    w=wave.open(nombre,'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(FS)
    w.writeframes(d.tobytes()); w.close()
D=180.0
casos=[
 ("A_pareja.wav",        D, lambda t: np.full(len(t),0.85)),
 ("B_intro_suave.wav",   D, lambda t: np.where(t<35,0.18,np.where(t<D-15,0.9,0.35))),
 ("C_se_apaga.wav",      D, lambda t: np.where(t<D*0.45,0.95,np.maximum(0.10,0.95-(t-D*0.45)/(D*0.55)*0.85))),
 ("D_muy_dinamica.wav",  D, lambda t: 0.25+0.70*np.abs(np.sin(2*np.pi*t/47.0))),
 ("E_corta.wav",        40.0, lambda t: np.full(len(t),0.8)),
 ("F_hueco_enmedio.wav", D, lambda t: np.where((t>70)&(t<110),0.02,0.85)),
]
for nom,dur,env in casos:
    escribe('wav/'+nom,dur,env); print('  ',nom)
escribe('wav/G_brillo_variable.wav', D, lambda t: np.full(len(t),0.85), lambda t: np.where(t<D*0.5,0.25,1.0))
print('   G_brillo_variable.wav')
