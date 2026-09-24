using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Authored voxel silhouettes. Coordinates are metres, Y up; hanging pieces attach at Y=0.
internal static class CaveAssetShapes
{
    internal sealed class Piece
    {
        internal string Name;
        internal EnchantedGroveVoxels Body, Moss;
        internal bool Solid, Hanging;
        internal Piece(string name, EnchantedGroveVoxels body, bool solid=false, bool hanging=false)
        { Name=name; Body=body; Solid=solid; Hanging=hanging; }
    }
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static float N(Vector3 p)=>EnchantedGroveVoxels.Noise(p.x*2.2f,p.y*2.2f,p.z*2.2f);
    static void Ground(EnchantedGroveVoxels g)
    { foreach(var p in g.Cells.Keys.Where(p=>p.y<0).ToArray())g.Cells.Remove(p); }
    static void Rock(EnchantedGroveVoxels g,Vector3 center,Vector3 radius,int seed)
    {
        // A primary mass with attached unequal lobes gives stratified rock instead of stacked cubes.
        g.Blob(center,radius,1,true,.27f);
        for(int i=0;i<8;i++)
        {
            float a=i*2.399f+seed*.71f;
            var offset=V(Mathf.Cos(a)*radius.x*.55f,Mathf.Sin(a*1.7f)*radius.y*.52f,Mathf.Sin(a)*radius.z*.55f);
            g.Blob(center+offset,Vector3.Scale(radius,V(.48f,.37f,.48f)),1,true,.32f);
        }
    }
    static EnchantedGroveVoxels Moss(EnchantedGroveVoxels source)
    {
        var moss=new EnchantedGroveVoxels(source.Step);
        foreach(var c in source.Cells)
        {
            var p=c.Key+Vector3Int.up;
            if(source.Cells.ContainsKey(p)||source.Cells.ContainsKey(p+Vector3Int.up))continue;
            var world=(Vector3)c.Key*source.Step;
            if(N(world+V(12,0,4))>.59f && world.y>0.12f)
                moss.Cells[p]=(byte)(10+Mathf.Min(2,(int)(N(world)*3)));
        }
        return moss;
    }
    static void Spike(EnchantedGroveVoxels g, Vector3 origin,float height,float radius,bool down,int seed)
    {
        int slices=Mathf.CeilToInt(height/g.Step);
        for(int i=0;i<slices;i++)
        {
            float t=i/(float)slices;
            float r=Mathf.Max(g.Step*.6f,radius*Mathf.Pow(1-t,1.15f)*(1+.10f*Mathf.Sin(t*31+seed)));
            var c=origin+V(Mathf.Sin(t*3+seed)*radius*.15f,(down?-1:1)*(i+.5f)*g.Step,Mathf.Sin(t*4+seed)*radius*.1f);
            g.Blob(c,V(r,g.Step*.65f,r*.83f),1,true,.18f);
        }
    }
    static void Root(EnchantedGroveVoxels g,Vector3 start,Vector3 end,float width,int seed)
    {
        Vector3 previous=start;
        for(int i=1;i<=18;i++)
        {
            float t=i/18f;
            var p=Vector3.Lerp(start,end,t)+V(Mathf.Sin(t*8+seed),0,Mathf.Sin(t*6+seed))*Mathf.Sin(t*Mathf.PI)*width*1.8f;
            float r=Mathf.Lerp(width,g.Step*.65f,t);
            g.Branch(previous,p,r+g.Step*.2f,r,(byte)(7+(i/4)%3));
            if(i==7||i==12)
            {
                var tip=p+V((seed%2==0?1:-1)*width*3,-width*3,width*1.5f);
                g.Branch(p,tip,r*.75f,g.Step*.55f,8);
            }
            previous=p;
        }
    }
    static void Mushroom(EnchantedGroveVoxels g,Vector3 origin,float height,float radius,int seed)
    {
        Vector3 neck=origin+V(height*.07f,height*.72f,0);
        g.Branch(origin,neck,Mathf.Max(.035f,height*.085f),height*.065f,13);
        // Underside and thick stepped dome, with the rim left broad and recognisable.
        g.Blob(neck,V(radius,.055f+height*.06f,radius*.9f),14,false,0);
        var cap=new EnchantedGroveVoxels(g.Step);
        cap.Blob(neck, V(radius,height*.29f,radius*.9f),16,true,.06f);
        foreach(var c in cap.Cells)
            if((c.Key.y+.5f)*g.Step>=neck.y)g.Cells[c.Key]=c.Value;
    }
    static void Crystal(EnchantedGroveVoxels g,Vector3 origin,float height,float radius,float lean)
    {
        int n=Mathf.CeilToInt(height/g.Step);
        for(int i=0;i<n;i++)
        {
            float t=i/(float)n,r=radius*(t<.68f?1:Mathf.Lerp(1,.08f,(t-.68f)/.32f));
            var c=origin+V(lean*t,(i+.5f)*g.Step,0);
            for(int x=Mathf.FloorToInt((c.x-r)/g.Step);x<=Mathf.CeilToInt((c.x+r)/g.Step);x++)
            for(int z=Mathf.FloorToInt((c.z-r)/g.Step);z<=Mathf.CeilToInt((c.z+r)/g.Step);z++)
            {
                float dx=Mathf.Abs((x+.5f)*g.Step-c.x)/r,dz=Mathf.Abs((z+.5f)*g.Step-c.z)/r;
                if(Mathf.Max(dx,dz)<=1 && dx+dz<1.5f)
                    g.Cells[new Vector3Int(x,Mathf.FloorToInt(c.y/g.Step),z)]=(byte)(19+(x+z+1000)%3);
            }
        }
    }
    static EnchantedGroveVoxels Fern(bool hanging)
    {
        var g=new EnchantedGroveVoxels(.035f);
        for(int frond=0;frond<9;frond++)
        {
            float a=frond*2.39996f;
            var dir=V(Mathf.Cos(a),0,Mathf.Sin(a));var side=V(-dir.z,0,dir.x);Vector3 previous=Vector3.zero;
            float length=.65f+(frond%3)*.18f;
            for(int j=1;j<=12;j++)
            {
                float t=j/12f;
                var p=dir*(hanging?Mathf.Sin(t*2.2f)*length*.6f:t*length)+Vector3.up*(hanging?-t*t*1.65f:Mathf.Sin(t*2.5f)*.55f);
                g.Branch(previous,p,.024f,.021f,10);
                for(int s=-1;s<=1;s+=2)
                    g.Branch(p,p+side*s*Mathf.Sin(t*Mathf.PI)*.19f-dir*.085f+Vector3.up*.03f,.042f,.018f,(byte)(11+j%2));
                previous=p;
            }
        }
        return g;
    }
    internal static List<Piece> Build()
    {
        var result=new List<Piece>();
        void Add(string name,EnchantedGroveVoxels g,bool solid=false,bool hanging=false,bool moss=false)
        {
            if(!hanging)Ground(g);
            var piece=new Piece(name,g,solid,hanging);
            if(moss){var layer=Moss(g);if(layer.Cells.Count>0)piece.Moss=layer;}
            result.Add(piece);
        }
        var b=new EnchantedGroveVoxels(.09f);Rock(b,V(0,1.2f,0),V(1.75f,1.4f,1.25f),1);Add("R01_Roca_redondeada",b,true,false,true);
        b=new EnchantedGroveVoxels(.09f);
        for(int i=0;i<8;i++)Rock(b,V(-1.15f+i*.31f,.22f+i*.30f,0),V(.64f,.48f,1.05f),i);
        Add("R02_Laja_inclinada",b,true,false,true);
        b=new EnchantedGroveVoxels(.10f);Rock(b,V(0,.2f,.1f),V(1.9f,.44f,.95f),4);Rock(b,V(0,-.40f,.58f),V(.80f,.65f,.40f),5);
        Add("R03_Saliente_pared",b,true,true,true);
        b=new EnchantedGroveVoxels(.10f);Rock(b,V(0,1.9f,0),V(.82f,2.1f,.74f),7);Rock(b,V(.58f,.66f,.10f),V(.59f,.80f,.65f),4);Add("R04_Pilar_rocoso",b,true,false,true);
        b=new EnchantedGroveVoxels(.12f);
        for(int s=-1;s<=1;s+=2)Rock(b,V(s*2.9f,1.65f,0),V(.85f,1.9f,1.0f),s+3);
        for(int i=0;i<=16;i++) {float a=i*Mathf.PI/16;Rock(b,V(Mathf.Cos(a)*2.9f,3+Mathf.Sin(a)*1.85f,0),V(.69f,.70f,.90f),i);}
        Add("R05_Arco_entrada",b,true,false,true);
        b=new EnchantedGroveVoxels(.04f);Rock(b,V(0,.1f,0),V(.45f,.15f,.36f),0);Add("P01_Piedra_plana",b);
        b=new EnchantedGroveVoxels(.04f);Spike(b,Vector3.zero,.65f,.32f,false,3);Add("P02_Fragmento_angular",b);
        b=new EnchantedGroveVoxels(.03f);for(int i=0;i<5;i++){float a=i*2.4f;Rock(b,V(Mathf.Cos(a)*.37f,.06f,Mathf.Sin(a)*.30f),V(.1f+i*.015f,.1f,.12f),i);}Add("P03_Grupo_grava",b);
        b=new EnchantedGroveVoxels(.045f);Rock(b,V(0,.29f,0),V(.66f,.47f,.48f),4);
        foreach(var p in b.Cells.Keys.Where(p=>Mathf.Abs((p.x+.5f)*b.Step-.16f*(p.y*b.Step))<.09f).ToArray())b.Cells.Remove(p);
        Add("P04_Roca_partida",b);
        for(int down=0;down<2;down++)for(int type=0;type<3;type++)
        {
            bool hanging=down==1;b=new EnchantedGroveVoxels(type==2?.065f:.055f);
            float height=type==0?1.15f:type==1?2.5f:3.4f;
            if(type==1&&hanging || type==2&&!hanging)
            {
                for(int k=0;k<3;k++)Spike(b,V((k-1)*.48f,0,(k%2)*.18f),height*(k==1?1:.65f),.44f,hanging,k+3);
            }
            else Spike(b,Vector3.zero,height,type==0?.48f:.58f,hanging,3);
            string[] names=hanging?new[]{"T01_Aguja_corta","T02_Racimo_tres_puntas","T03_Estalactita_larga"}:new[]{"E01_Cono_bajo","E02_Aguja_alta","E03_Racimo_escalonado"};
            Add(names[type],b,true,hanging);
        }
        for(int type=0;type<4;type++)
        {
            b=new EnchantedGroveVoxels(.04f);
            if(type<2)
            {
                int count=type==0?4:7;float length=type==0?1.4f:3.1f;
                b.Branch(V(-.55f,-.05f,0),V(.55f,-.05f,0),.14f,.13f,7);
                for(int k=0;k<count;k++){float x=(k-(count-1)*.5f)*.17f;Root(b,V(x,0,0),V(x+.13f*Mathf.Sin(k),-length*(.67f+.11f*(k%4)),.12f*Mathf.Cos(k)),.10f,k);}
            }
            else if(type==2)
            {
                Root(b,V(-.4f,0,.05f),V(.25f,-2.8f,.1f),.16f,5);
                for(int k=0;k<8;k++)Root(b,V(-.4f+k*.07f,-.25f-k*.27f,.08f),V((k%2==0?-1:1)*(1.1f+.11f*(k%3)),-.6f-k*.3f,.07f),.075f,k);
            }
            else
            {
                b.Blob(V(0,.25f,0),V(.25f,.36f,.24f),7,true,.2f);
                for(int k=0;k<7;k++){float a=k*2.399f;Root(b,V(0,.26f,0),V(Mathf.Cos(a)*1.15f,.04f,Mathf.Sin(a)*.85f),.13f,k);}
            }
            Add(new[]{"Z01_Raiz_colgante","Z02_Cortina_raices","Z03_Raices_pared","Z04_Raiz_suelo"}[type],b,false,type<3);
        }
        for(int type=0;type<4;type++)
        {
            b=new EnchantedGroveVoxels(.025f);
            if(type==3)for(int k=0;k<5;k++){float a=k*2.399f;Mushroom(b,V(Mathf.Cos(a)*.32f,0,Mathf.Sin(a)*.28f),.30f+k*.13f,.13f+k*.035f,k);}
            else Mushroom(b,Vector3.zero,type==0?.30f:type==1?.58f:1.05f,type==0?.20f:type==1?.46f:.28f,1);
            Add(new[]{"H01_Sombrero_pequeno","H02_Sombrero_ancho","H03_Hongo_alto","H04_Colonia"}[type],b);
        }
        b=new EnchantedGroveVoxels(.04f);
        for(int k=0;k<11;k++){float a=k*2.399f;b.Blob(V(Mathf.Cos(a)*.42f,.045f,Mathf.Sin(a)*.32f),V(.32f,.055f,.27f),10,true,.25f);}
        Add("V01_Placa_musgo",b);Add("V02_Helecho_compacto",Fern(false));Add("V03_Helecho_colgante",Fern(true),false,true);
        for(int type=0;type<3;type++)
        {
            b=new EnchantedGroveVoxels(.04f);
            if(type==0)Crystal(b,Vector3.zero,1.35f,.21f,.18f);
            else
            {
                Crystal(b,Vector3.zero,type==1?.8f:2.05f,type==1?.18f:.28f,.13f);
                for(int k=0;k<4;k++){float a=k*2.399f;Crystal(b,V(Mathf.Cos(a)*.36f,0,Mathf.Sin(a)*.32f),(type==1?.40f:.75f)+k*.12f,.13f,Mathf.Cos(a)*.20f);}
            }
            Add(new[]{"C01_Cristal_solitario","C02_Racimo_bajo","C03_Racimo_alto"}[type],b);
        }
        return result;
    }
}
