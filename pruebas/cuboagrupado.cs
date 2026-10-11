using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
class CuboAgrupado {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static byte[] actual; static int calls;
 public static int Draw(IntPtr self,uint type,uint tris,IntPtr data,uint stride){
  calls++;actual=new byte[tris*3*stride];Marshal.Copy(data,actual,0,actual.Length);return 0;
 }
 static void Set(Type t,object o,string name,object v){t.GetField(name,F).SetValue(o,v);}
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Main(string[] args){
  Assembly asm=Assembly.LoadFrom(args.Length>0?args[0]:"rocola.exe");Type dev=asm.GetType("Rocola.D3D9Device");
  Type host=asm.GetType("Rocola.GpuStarfield");
  bool hd=host.GetField("cubeBatchV",F)!=null; if(!hd)host=dev;
  Type vtx=asm.GetType("Rocola.D3D9+Vtx");
  object device=FormatterServices.GetUninitializedObject(dev);
  Set(dev,device,"dev",new IntPtr(1));Set(dev,device,"stBlend",3);
  FieldInfo draw=dev.GetField("drawUp",F);
  draw.SetValue(device,Delegate.CreateDelegate(draw.FieldType,typeof(CuboAgrupado).GetMethod("Draw")));
  object owner=hd?FormatterServices.GetUninitializedObject(host):device;
  if(hd)Set(host,owner,"gpu",device);
  Set(host,owner,"cbx",new double[4]);Set(host,owner,"cbz",new double[4]);Set(host,owner,"cbo",new int[4]);
  Set(host,owner,"cubeFaceV",Array.CreateInstance(vtx,6));Set(host,owner,"cubeBatchV",Array.CreateInstance(vtx,24));
  Set(host,owner,hd?"BlankCubeColor":"CubeBaseColor",0x00384F62u);
  int tested=0;int size=Marshal.SizeOf(vtx);
  for(int textured=0;textured<2;textured++)foreach(float alpha in new float[]{.004f,.17f,.5f,1f})
  for(int angleStep=0;angleStep<64;angleStep++){
   double angle=angleStep*.137,tilt=(angleStep%9-4)*.04;
   float ax=13,ay=27,aw=173,ah=194,um=.73f,vm=.81f;
   double cx=ax+aw/2.0,cy=ay+ah/2.0,r=aw/2.0,deg=Math.PI/180;
   double[] xs=new double[4],zs=new double[4];int[] order={0,1,2,3};
   for(int k=0;k<4;k++){xs[k]=cx+r*Math.Cos(angle+45*deg+k*90*deg);zs[k]=Math.Sin(angle+90*deg+k*90*deg);}
   for(int i=0;i<4;i++)for(int j=0;j<3-i;j++)if(zs[order[j]]>zs[order[j+1]]){int tmp=order[j];order[j]=order[j+1];order[j+1]=tmp;}
   List<byte> expected=new List<byte>();
   foreach(int k in order){
    if(zs[k]<=0)continue;
    float left=(float)Math.Round(Math.Min(xs[k],xs[(k+1)%4])),right=(float)Math.Round(Math.Max(xs[k],xs[(k+1)%4]));
    if(right-left<1)continue;
    int dark=(int)((1-zs[k])*130);dark=Math.Max(0,Math.Min(255,dark));
    uint g=(uint)(255-dark),av=(uint)(alpha*255f+.5f);
    uint color=textured!=0?(av<<24)|(g<<16)|(g<<8)|g:(av<<24)|((0x38u*g/255u)<<16)|((0x4Fu*g/255u)<<8)|(0x62u*g/255u);
    Array face=Array.CreateInstance(vtx,6);
    host.GetMethod("CubeFace",F).Invoke(null,new object[]{face,left,ay,right,ay+ah,cx,cy,Math.Cos(tilt),Math.Sin(tilt),um,vm,color});
    GCHandle pin=GCHandle.Alloc(face,GCHandleType.Pinned);
    try{byte[] bytes=new byte[6*size];Marshal.Copy(pin.AddrOfPinnedObject(),bytes,0,bytes.Length);expected.AddRange(bytes);}
    finally{pin.Free();}
   }
   IntPtr tex=new IntPtr(textured);
   Set(dev,device,"stTex",tex);Set(dev,device,"stTss",textured!=0?2:1);
   FieldInfo direccion=dev.GetField("stDireccion",F);if(direccion!=null)direccion.SetValue(device,1); // CLAMP ya establecido por InitStates
   actual=null;calls=0;
   host.GetMethod("DrawCube",F).Invoke(owner,new object[]{tex,um,vm,ax,ay,aw,ah,angle,alpha,tilt});
   Check(calls==1,"No agrupa en un envio");
   Check(actual!=null&&actual.Length==expected.Count,"Cantidad de vertices distinta");
   for(int i=0;i<actual.Length;i++)Check(actual[i]==expected[i],"Cambio de geometria/UV/color/orden");
   tested++;
  }
  Set(dev,device,"dev",IntPtr.Zero); // dispositivo simulado; nunca liberar un puntero de prueba
  Console.WriteLine("  OK    "+tested+" cubos, vertices/UV/color/orden byte-identicos; un envio por cubo.");
 }
}
