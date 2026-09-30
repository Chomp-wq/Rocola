"""Evalua PCM real de SpectralTap mediante renderdsee.exe; nunca activa modelos.
Manifest JSON: {"pares":[{"referencia":"master.wav","degradada":"codec.wav","genero":"rock",
"degradacion_conocida":true,"tipo":"musica","referencia_sin_perdidas":true,
"alineacion_verificada":true}]}.
Los WAV deben estar alineados y tener igual frecuencia/canales. Maximo 20 pares de 30 s.
"""
import argparse
import hashlib
import json
import os
import struct
import subprocess
import tempfile
import time
import numpy as np

def sha(ruta):
    h=hashlib.sha256()
    with open(ruta,"rb") as f:
        for b in iter(lambda:f.read(1048576),b""): h.update(b)
    return h.hexdigest()

def leer_wav(ruta, max_seg=30, con_info=False):
    with open(ruta,"rb") as f:
        if f.read(4)!=b"RIFF": raise ValueError("Solo WAV RIFF")
        longitud=f.read(4)
        if len(longitud)!=4: raise ValueError("RIFF truncado")
        tam_riff=struct.unpack("<I",longitud)[0]
        fin=tam_riff+8
        if fin>os.fstat(f.fileno()).st_size or fin<12: raise ValueError("RIFF truncado")
        if f.read(4)!=b"WAVE": raise ValueError("WAV invalido")
        fmt=None
        while True:
            cab=f.read(8)
            if len(cab)!=8: raise ValueError("WAV sin audio")
            tipo,tam=struct.unpack("<4sI",cab)
            if f.tell()+tam+(tam%2)>fin: raise ValueError("Chunk fuera de RIFF")
            if tipo==b"fmt ":
                if tam>4096: raise ValueError("Cabecera excesiva")
                fmt=f.read(tam)
            elif tipo==b"data":
                if fmt is None or len(fmt)<16: raise ValueError("WAV sin formato")
                tag,ch,fs,_,bloque,bits=struct.unpack("<HHIIHH",fmt[:16])
                if tag==65534 and len(fmt)>=40: tag=struct.unpack("<H",fmt[24:26])[0]
                if ch not in (1,2) or not 8000<=fs<=96000: raise ValueError("Formato fuera de presupuesto")
                permitidos={(1,16):"<i2",(1,32):"<i4",(3,32):"<f4"}
                if (tag,bits) not in permitidos: raise ValueError("WAV PCM16/32 o float32 requerido")
                if bloque!=ch*(bits//8) or tam%bloque: raise ValueError("Alineacion WAV invalida")
                cuenta=min(tam//bloque,int(fs*max_seg))
                bruto=f.read(cuenta*bloque)
                if len(bruto)!=cuenta*bloque: raise ValueError("WAV truncado")
                x=np.frombuffer(bruto,dtype=permitidos[(tag,bits)]).astype(np.float64).reshape(-1,ch)
                if tag==1: x/=float(2**(bits-1))
                if len(x)<fs//10 or not np.isfinite(x).all(): raise ValueError("WAV <0.1s/no finito")
                info={"cuadros_totales":tam//bloque,"cuadros_evaluados":cuenta,
                      "truncado_a_30s":cuenta<tam//bloque}
                return (x,fs,info) if con_info else (x,fs)
            else: f.seek(tam,1)
            if tam%2: f.seek(1,1)

def escribir_float(ruta,x,fs):
    if (x.ndim!=2 or x.shape[1] not in (1,2) or not 8000<=fs<=96000 or
            not len(x) or not np.isfinite(x).all()): raise ValueError("PCM invalido")
    x=x.astype("<f4")
    if not np.isfinite(x).all(): raise ValueError("PCM no representable float32")
    bruto=x.tobytes(); ch=x.shape[1]
    with open(ruta,"xb") as f:
        f.write(b"RIFF"+struct.pack("<I",len(bruto)+50)+b"WAVE")
        f.write(b"fmt "+struct.pack("<IHHIIHHH",18,3,ch,fs,fs*ch*4,ch*4,32,0))
        f.write(b"fact"+struct.pack("<II",4,len(x))+b"data"+struct.pack("<I",len(bruto))+bruto)

def metricas(x,ref,fs):
    if (x.shape!=ref.shape or len(x)<fs//10 or not np.isfinite(x).all() or
            not np.isfinite(ref).all()): raise ValueError("PCM incompatible/corto/no finito")
    # Medir 10 ms de envolvente por canal: ataques conservados sin depender de fase exacta.
    hop=max(1,fs//100); n=len(x)//hop
    ex=np.sqrt(np.mean(x[:n*hop].reshape(n,hop,-1)**2,axis=1)+1e-12)
    er=np.sqrt(np.mean(ref[:n*hop].reshape(n,hop,-1)**2,axis=1)+1e-12)
    ataques=np.any(er[1:]>er[:-1]*2,axis=1)
    diferencia=(20*np.log10(ex+1e-8)-20*np.log10(er+1e-8))**2
    espectro=[]
    largo=min(2048,len(x)); win=np.hanning(largo)
    for p in range(0,len(x)-largo+1,largo//2):
        a=np.abs(np.fft.rfft(x[p:p+largo]*win[:,None],axis=0))**2
        b=np.abs(np.fft.rfft(ref[p:p+largo]*win[:,None],axis=0))**2
        espectro.append((10*np.log10(a+1e-10)-10*np.log10(b+1e-10))**2)
    e=np.mean(espectro,axis=0); f=np.fft.rfftfreq(largo,1/fs)
    bandas=[float(e[sel].mean()) if sel.any() else 0 for sel in (f<8000,f>=8000)]
    if x.shape[1]==2:
        # Piso relativo al cuerpo total: no premiar ni castigar dB de un side casi nulo.
        piso=max(1e-10,float(np.mean(ref**2))*1e-4)
        px=max(piso,float(np.mean((x[:,0]-x[:,1])**2)))
        pr=max(piso,float(np.mean((ref[:,0]-ref[:,1])**2)))
        estereo=float(abs(10*np.log10(px/pr)))
        side_mse=float(np.mean(((x[:,0]-x[:,1])-(ref[:,0]-ref[:,1]))**2))
    else: estereo=0.; side_mse=0.
    return {"mse_pcm":float(np.mean((x-ref)**2)),"mse_db_espectro":bandas,
            "mse_envolvente_db":float(diferencia.mean()),
            "mse_ataques_db":float(diferencia[1:][ataques].mean()) if ataques.any() else None,
            "error_side_db":estereo,"mse_side":side_mse,"pico_muestra":float(np.max(np.abs(x)))}

def regresiones(candidato,instalado):
    bloqueos=[]
    for clave in ("mse_pcm","mse_envolvente_db","mse_ataques_db","mse_side"):
        a,b=candidato[clave],instalado[clave]
        if a is not None and b is not None and a>b*1.1+1e-10: bloqueos.append(clave)
    if any(a>b*1.1+1e-10 for a,b in zip(candidato["mse_db_espectro"],instalado["mse_db_espectro"])):
        bloqueos.append("espectro")
    if candidato["error_side_db"]>instalado["error_side_db"]+.5: bloqueos.append("imagen_estereo")
    if candidato["pico_muestra"]>max(1.,instalado["pico_muestra"]*1.01): bloqueos.append("pico")
    return bloqueos

def evaluar(manifest,renderer,rocola,candidato,instalado,destino,frame=2048):
    with open(manifest,encoding="utf-8") as f: pares=json.load(f)["pares"]
    if not 1<=len(pares)<=20: raise ValueError("Presupuesto 1..20 pares")
    os.mkdir(destino)
    base=os.path.dirname(os.path.abspath(manifest)); resultados=[]; bloqueos=[]; duraciones=[]
    with tempfile.TemporaryDirectory(prefix="rocola-pcm-") as td:
        for i,par in enumerate(pares):
            referencia=os.path.abspath(os.path.join(base,par["referencia"]))
            degradada=os.path.abspath(os.path.join(base,par["degradada"]))
            r,fs,info=leer_wav(referencia,con_info=True); d,fds,info_d=leer_wav(degradada,con_info=True)
            if fs!=fds or r.shape!=d.shape or info["cuadros_totales"]!=info_d["cuadros_totales"]:
                raise ValueError("Pares no alineados/duracion completa distinta")
            entrada=os.path.join(td,str(i)+"-entrada.wav"); escribir_float(entrada,d,fs)
            salidas=[]
            for j,modelo in enumerate((candidato,instalado)):
                salida=os.path.join(td,"%d-%d.wav"%(i,j)); inicio=time.monotonic()
                subprocess.run([renderer,rocola,modelo,entrada,salida,str(frame)],check=True,
                               stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,stderr=subprocess.PIPE,
                               timeout=90,creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
                duraciones.append(time.monotonic()-inicio)
                x,fx=leer_wav(salida)
                if fx!=fs: raise ValueError("Renderer cambio formato")
                salidas.append(x)
            metrics={nombre:metricas(x,r,fs) for nombre,x in zip(("candidato","instalado","degradada"),salidas+[d])}
            bloqueos.extend("%d:%s"%(i,b) for b in regresiones(metrics["candidato"],metrics["instalado"]))
            # A/B: RMS comun y atenuacion comun, sin limitar distinto cada version.
            ro=np.sqrt(np.mean(d*d)); ab=[d]+salidas
            ab=[x*(ro/max(1e-12,np.sqrt(np.mean(x*x)))) for x in ab]
            margen=min(1.,.95/max(1e-12,max(np.max(np.abs(x)) for x in ab)))
            for nombre,x in zip(("original","candidato","instalado"),ab):
                escribir_float(os.path.join(destino,"%02d-%s.wav"%(i,nombre)),x*margen,fs)
            resultados.append({"par":i,"genero":par.get("genero","desconocido"),"origen":par.get("origen","no_verificado"),"metricas":metrics,
                               "referencia_sha256":sha(referencia),"degradada_sha256":sha(degradada),
                               "ventana_evaluada":info,"segundos_evaluados":len(r)/fs})
    error=np.mean([r["metricas"]["candidato"]["mse_db_espectro"][1] for r in resultados])
    referencia=np.mean([r["metricas"]["instalado"]["mse_db_espectro"][1] for r in resultados])
    validado=all(p.get("degradacion_conocida") is True for p in pares)
    musical=all(p.get("tipo")=="musica" and p.get("referencia_sin_perdidas") is True and
                p.get("alineacion_verificada") is True for p in pares)
    numerico=bool(validado and not bloqueos and error<referencia*.98)
    informe={"formato":"ROCOLA-PCM-DSEE 1","candidato_sha256":sha(candidato),"instalado_sha256":sha(instalado),
             "pares":resultados,"degradaciones_conocidas":validado,"bloqueos":bloqueos,
             "supera_guard_numerico_en_pares":numerico,"pares_musicales_verificados_declarados":musical,
             "supera_politica_pcm":bool(numerico and musical),
             "tiempo_render_s":sum(duraciones),"escucha_pendiente":True,"activado":False,
             "limites":["Requiere pares realmente alineados y referencia sin perdidas comprobada",
                        "Origen lossless/alineacion se declaran en el manifest, no se deducen de un WAV",
                        "Un fixture sintetico no es evidencia musical ni anula un rechazo de envolvente",
                        "RMS no equivale a sonoridad percibida","Metricas PCM no demuestran preferencia audible"]}
    with open(os.path.join(destino,"informe_pcm.json"),"x",encoding="utf-8") as f: json.dump(informe,f,sort_keys=True,indent=2,allow_nan=False)
    return informe

if __name__=="__main__":
    ap=argparse.ArgumentParser(description=__doc__)
    for n in ("manifest","renderer","rocola","candidato","instalado","destino"): ap.add_argument("--"+n,required=True)
    ap.add_argument("--frame",type=int,choices=(2048,4096),default=2048)
    a=ap.parse_args(); print(json.dumps(evaluar(a.manifest,a.renderer,a.rocola,a.candidato,a.instalado,a.destino,a.frame),sort_keys=True))
