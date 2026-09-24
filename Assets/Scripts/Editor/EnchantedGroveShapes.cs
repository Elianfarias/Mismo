using System;
using UnityEngine;

// Original authored silhouettes; seeded variation is shared across the whole kit.
internal static class EnchantedGroveShapes
{
    static Vector3 V(float x,float y,float z) => new Vector3(x,y,z);
    static float R(System.Random random,float min,float max) => Mathf.Lerp(min,max,(float)random.NextDouble());
    internal static EnchantedGroveVoxels Tree(int seed,byte leaf,bool slender=false)
    {
        var g=new EnchantedGroveVoxels(slender?.1f:.12f);var random=new System.Random(seed);
        g.Branch(V(0,.1f,0),V(.18f,2.4f,.05f),.29f,.19f,13);
        g.Branch(V(.18f,2.3f,.05f),V(-.15f,4.6f,.13f),.2f,.07f,14);
        for(int r=0;r<5;r++){float angle=r*Mathf.PI*2/5;g.Branch(V(0,.4f,0),V(Mathf.Cos(angle)*.68f,.04f,Mathf.Sin(angle)*.68f),.18f,.06f,13);}
        if(slender)
        {
            for(int layer=0;layer<7;layer++)
            {
                float y=1.4f+layer*.62f,rad=1.1f-layer*.115f;
                for(int i=0;i<5;i++){float a=i*1.257f+layer*.8f;var end=V(Mathf.Cos(a)*rad*.57f,y,Mathf.Sin(a)*rad*.57f);g.Branch(V(0,y-.3f,0),end,.1f,.06f,13);g.Blob(end,V(rad*.73f,.58f,rad*.7f),leaf,true,.15f);}
            }
            g.Blob(V(0,5.75f,0),V(.32f,.7f,.32f),leaf);
        }
        else
        {
            for(int branch=0;branch<8;branch++)
            {
                float angle=branch*2.39996f,spread=branch<5?1.55f:1.05f;
                float y=branch<5?3.2f+branch*.16f:4.45f+(branch-5)*.21f;
                var end=V(Mathf.Cos(angle)*spread,y,Mathf.Sin(angle)*spread);
                g.Branch(V(.12f,1.6f+branch*.23f,0),end,.18f,.065f,14);
                g.Blob(end,V(1.02f,.74f,1.0f),leaf,true,.2f);
                for(int k=0;k<5;k++)
                {
                    float a=k*1.257f+branch;
                    var p=end+V(Mathf.Cos(a)*.74f,R(random,-.3f,.5f),Mathf.Sin(a)*.74f);
                    g.Blob(p,V(R(random,.36f,.61f),R(random,.3f,.52f),R(random,.38f,.62f)),leaf,true,.2f);
                }
            }
            g.Blob(V(-.2f,5.1f,.1f),V(1.2f,.8f,1.12f),leaf,true,.18f);
        }
        // Flatten roots at ground level.
        foreach(var p in new System.Collections.Generic.List<Vector3Int>(g.Cells.Keys))if(p.y<0)g.Cells.Remove(p);
        return g;
    }
    internal static EnchantedGroveVoxels Shrub(byte color,int seed)
    {
        var g=new EnchantedGroveVoxels(.08f);var random=new System.Random(seed);
        for(int i=0;i<7;i++)
        {
            float a=i*2.399f;var p=V(Mathf.Cos(a)*.48f,R(random,.4f,.8f),Mathf.Sin(a)*.48f);
            g.Branch(Vector3.zero,p,.07f,.045f,13);g.Blob(p,V(.49f,.42f,.47f),color,true,.2f);
        }
        return g;
    }
    internal static EnchantedGroveVoxels Fern()
    {
        var g=new EnchantedGroveVoxels(.06f);
        for(int i=0;i<9;i++)
        {
            float a=i*2.399f;var dir=V(Mathf.Cos(a),0,Mathf.Sin(a));var side=V(-dir.z,0,dir.x);Vector3 prev=V(0,.02f,0);
            for(int j=1;j<=7;j++)
            {
                float t=j/7f;var p=dir*(t*.85f)+Vector3.up*(Mathf.Sin(t*2.4f)*.63f+.05f);
                g.Branch(prev,p,.035f,.03f,2);
                float width=Mathf.Sin(t*Mathf.PI)*.23f;
                for(int s=-1;s<=1;s+=2)g.Branch(p,p+side*s*width-dir*.1f+Vector3.up*.06f,.055f,.025f,(byte)(j%2==0?2:3));
                prev=p;
            }
        }
        return g;
    }
    internal static EnchantedGroveVoxels Flowers(byte petal,bool reeds=false,bool grass=false)
    {
        var g=new EnchantedGroveVoxels(.05f);var random=new System.Random(petal+61);
        for(int i=0;i<(grass?17:9);i++)
        {
            float x=R(random,-.48f,.48f),z=R(random,-.38f,.38f),h=R(random,.32f,reeds?1.45f:grass?.7f:.95f);
            var p=V(x,h,z);g.Branch(V(x,0,z),p,.032f,.025f,2);
            var end=p+V(R(random,-.23f,.23f),-.12f,R(random,-.2f,.2f));
            g.Branch(p*.5f+V(x*.5f,0,z*.5f),end,.055f,.025f,3);
            if(grass)continue;
            if(reeds)g.Box(p-V(.055f,0,.055f),p+V(.055f,.27f,.055f),14);
            else if(petal==26)
            {
                for(int k=0;k<4;k++)g.Blob(p+V(0,k*.07f,0),V(.09f,.055f,.08f),26,false,0);
            }
            else
            {
                g.Box(p-V(.14f,.025f,.055f),p+V(.14f,.04f,.055f),petal);
                g.Box(p-V(.055f,.025f,.14f),p+V(.055f,.04f,.14f),petal);
                g.Box(p-V(.04f,-.045f,.04f),p+V(.04f,.1f,.04f),22);
            }
        }
        return g;
    }
    internal static EnchantedGroveVoxels Boulder(float scale,int seed)
    {
        var g=new EnchantedGroveVoxels(.12f);var random=new System.Random(seed);
        g.Blob(V(0,scale*.52f,0),V(scale,scale*.65f,scale*.82f),10,true,.32f);
        g.Blob(V(scale*.55f,scale*.28f,-scale*.17f),V(scale*.55f,scale*.36f,scale*.5f),10,true,.18f);
        for(int i=0;i<6;i++)g.Blob(V(R(random,-.5f,.5f)*scale,R(random,.92f,1.02f)*scale,R(random,-.4f,.4f)*scale),V(scale*.39f,.11f,scale*.3f),29,true,.16f);
        foreach(var p in new System.Collections.Generic.List<Vector3Int>(g.Cells.Keys))if(p.y<0)g.Cells.Remove(p);
        return g;
    }
    internal static EnchantedGroveVoxels Cliff()
    {
        var g=new EnchantedGroveVoxels(.16f);
        for(int y=0;y<8;y++)for(int x=0;x<4;x++)
        {
            float px=(x-1.5f)*.96f+(y%2)*.15f;
            g.Blob(V(px,.3f+y*.56f,.1f),V(.68f,.43f,.65f+(float)Math.Sin(x+y)*.15f),10,true,.22f);
        }
        g.Box(V(-1.95f,4.53f,-.58f),V(2.04f,4.68f,.53f),29);
        for(int i=0;i<6;i++)g.Blob(V(-1.8f+i*.68f,4.68f,-.1f),V(.5f,.12f,.5f),29);
        return g;
    }
    internal static EnchantedGroveVoxels Vines(bool longer)
    {
        var g=new EnchantedGroveVoxels(.07f);
        for(int strand=0;strand<5;strand++)
        {
            float length=(longer?2.4f:1.25f)*(1-.12f*(strand%3));float x=(strand-2)*.28f;
            Vector3 prev=V(x,0,0);
            for(int k=1;k<=16;k++)
            {
                float t=k/16f;var p=V(x+Mathf.Sin(t*7+strand)*.13f,-t*length,Mathf.Cos(t*5+strand)*.08f);
                g.Branch(prev,p,.04f,.035f,1);
                g.Blob(p+V((k%2==0?1:-1)*.1f,-.025f,-.05f),V(.15f,.13f,.07f),(byte)(strand%2==0?1:29),true,.1f);prev=p;
            }
        }
        return g;
    }
    internal static EnchantedGroveVoxels Arch()
    {
        var g=new EnchantedGroveVoxels(.1f);
        for(int side=-1;side<=1;side+=2)
        {
            for(int row=0;row<5;row++)
            {
                float x=side*1.75f;g.Box(V(x-.39f,row*.49f,-.43f),V(x+.39f,row*.49f+.46f,.43f),(byte)(10+row%3));
            }
            g.Box(V(side*1.75f-.51f,0,-.53f),V(side*1.75f+.51f,.24f,.53f),11);
            g.Box(V(side*1.75f-.48f,2.4f,-.5f),V(side*1.75f+.48f,2.68f,.5f),12);
        }
        for(int i=-5;i<=5;i++)
        {
            float x=i*.32f,y=2.65f+Mathf.Sqrt(Mathf.Max(0,1-x*x/(1.8f*1.8f)))*1.1f;
            g.Box(V(x-.17f,Mathf.Abs(i)==5?2.55f:y-.22f,-.45f),V(x+.17f,y+.39f,.45f),(byte)(10+(i+6)%3));
        }
        g.Blob(V(-1.73f,2.68f,.1f),V(.5f,.12f,.47f),29);
        g.Blob(V(-.4f,4.12f,0),V(.8f,.12f,.48f),29);return g;
    }
    internal static EnchantedGroveVoxels Pillar()
    {
        var g=new EnchantedGroveVoxels(.1f);
        g.Box(V(-.65f,0,-.65f),V(.65f,.25f,.65f),10);g.Box(V(-.5f,.25f,-.5f),V(.5f,.45f,.5f),12);
        for(int i=0;i<5;i++)g.Box(V(-.36f,.45f+i*.35f,-.36f),V(.36f,.78f+i*.35f,.36f),(byte)(10+i%3));
        g.Box(V(-.37f,2.18f,-.37f),V(.15f,2.5f,.38f),11);g.Blob(V(-.12f,2.48f,0),V(.28f,.1f,.3f),29);return g;
    }
    internal static EnchantedGroveVoxels Wall()
    {
        var g=new EnchantedGroveVoxels(.1f);
        for(int row=0;row<4;row++)for(int col=0;col<5-row/2;col++)
        {
            float x=-1.7f+col*.7f+(row%2)*.18f;g.Box(V(x,row*.4f,-.32f),V(x+.65f,row*.4f+.37f,.32f),(byte)(10+(col+row)%3));
        }
        g.Blob(V(-.6f,1.62f,.04f),V(.74f,.1f,.33f),29);return g;
    }
    internal static EnchantedGroveVoxels Steps()
    {
        var g=new EnchantedGroveVoxels(.1f);
        for(int i=0;i<5;i++)g.Box(V(-1,0,-1+i*.42f),V(1,(i+1)*.2f,-.6f+i*.42f),(byte)(10+i%3));return g;
    }
    internal static EnchantedGroveVoxels Path()
    {
        var g=new EnchantedGroveVoxels(.08f);
        for(int i=0;i<3;i++)g.Blob(V(i%2*.16f,.06f,-.6f+i*.6f),V(.42f,.1f,.25f),10,true,.12f);return g;
    }
    internal static EnchantedGroveVoxels Lilies()
    {
        var g=new EnchantedGroveVoxels(.05f);
        for(int i=0;i<3;i++)
        {
            var p=V((i-1)*.42f,.025f,(i%2)*.4f);g.Blob(p,V(.28f,.027f,.25f),2,false,0);
            if(i!=1)continue;
            for(int j=0;j<6;j++){float a=j*1.047f;g.Blob(p+V(Mathf.Cos(a)*.07f,.09f,Mathf.Sin(a)*.07f),V(.07f,.06f,.07f),9,false,0);}
            g.Box(p+V(-.03f,.13f,-.03f),p+V(.03f,.18f,.03f),22);
        }
        return g;
    }
    internal static EnchantedGroveVoxels Mushrooms()
    {
        var g=new EnchantedGroveVoxels(.05f);
        for(int i=0;i<3;i++)
        {
            float x=(i-1)*.3f,z=i%2*.24f,h=.27f+i*.08f;g.Box(V(x-.05f,0,z-.05f),V(x+.05f,h,z+.05f),17);
            g.Blob(V(x,h,z),V(.18f,.1f,.17f),7,false,.1f);g.Box(V(x-.035f,h+.08f,z-.035f),V(x+.035f,h+.12f,z+.035f),25);
        }
        return g;
    }
    internal static EnchantedGroveVoxels Lantern(bool post)
    {
        var g=new EnchantedGroveVoxels(.05f);float baseY=post?1.6f:.2f;
        if(post){g.Box(V(-.12f,0,-.12f),V(.12f,1.85f,.12f),13);g.Box(V(-.13f,1.82f,-.1f),V(.6f,1.95f,.1f),14);}
        float x=post?.52f:0;
        g.Box(V(x-.2f,baseY-.03f,-.2f),V(x+.2f,baseY+.09f,.2f),13);
        g.Box(V(x-.13f,baseY+.09f,-.13f),V(x+.13f,baseY+.46f,.13f),27);
        for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2)g.Box(V(x+a*.16f-.025f,baseY+.08f,b*.16f-.025f),V(x+a*.16f+.025f,baseY+.5f,b*.16f+.025f),13);
        g.Box(V(x-.24f,baseY+.48f,-.24f),V(x+.24f,baseY+.56f,.24f),14);
        g.Box(V(x-.15f,baseY+.56f,-.15f),V(x+.15f,baseY+.65f,.15f),13);return g;
    }
    internal static EnchantedGroveVoxels Bridge()
    {
        var g=new EnchantedGroveVoxels(.1f);
        for(int i=0;i<13;i++){float z=-2+i*.32f,y=.15f+Mathf.Sin(i/12f*Mathf.PI)*.38f;g.Box(V(-.8f,y,z),V(.8f,y+.15f,z+.28f),(byte)(13+i%3));}
        for(int side=-1;side<=1;side+=2)
        {
            for(int i=0;i<5;i++){float z=-1.9f+i*.94f;g.Box(V(side*.87f-.075f,0,z-.08f),V(side*.87f+.075f,1.35f,z+.08f),13);}
            g.Branch(V(side*.87f,1.1f,-1.9f),V(side*.87f,1.45f,0),.065f,.065f,14);g.Branch(V(side*.87f,1.45f,0),V(side*.87f,1.1f,1.9f),.065f,.065f,14);
        }
        return g;
    }
    internal static EnchantedGroveVoxels Cottage()
    {
        var g=new EnchantedGroveVoxels(.1f);
        g.Box(V(-2.4f,0,-2),V(2.4f,.3f,2.1f),10);
        // Hollow shell with closed decorative entrance; this is an exterior prop.
        g.Box(V(-2.1f,.3f,-1.8f),V(2.1f,3.05f,-1.6f),17);g.Box(V(-2.1f,.3f,1.7f),V(2.1f,3.05f,1.9f),17);
        g.Box(V(-2.1f,.3f,-1.8f),V(-1.9f,3.05f,1.9f),16);g.Box(V(1.9f,.3f,-1.8f),V(2.1f,3.05f,1.9f),16);
        for(int s=-1;s<=1;s+=2)
        {
            g.Box(V(s*2-.1f,.3f,-1.9f),V(s*2+.1f,3.2f,2),13);
            g.Box(V(-2.2f,.75f,s*1.8f-.08f),V(2.2f,.94f,s*1.8f+.08f),14);
            g.Box(V(-2.2f,2.78f,s*1.8f-.08f),V(2.2f,2.98f,s*1.8f+.08f),13);
        }
        g.Box(V(-.49f,.3f,-1.96f),V(.49f,2.05f,-1.72f),13);g.Box(V(-.38f,.32f,-2.0f),V(.38f,1.93f,-1.95f),15);
        for(int i=-1;i<=1;i++)g.Box(V(i*.23f-.025f,.33f,-2.05f),V(i*.23f+.025f,1.95f,-2.0f),14);
        g.Box(V(.24f,1.03f,-2.1f),V(.34f,1.13f,-2.05f),22);
        foreach(float x in new[]{-1.32f,1.32f})
        {
            g.Box(V(x-.45f,1.17f,-1.98f),V(x+.45f,2.25f,-1.86f),13);g.Box(V(x-.34f,1.3f,-2.0f),V(x+.34f,2.14f,-1.97f),27);
            g.Box(V(x-.04f,1.25f,-2.04f),V(x+.04f,2.18f,-2.0f),14);g.Box(V(x-.39f,1.65f,-2.04f),V(x+.39f,1.74f,-2.0f),14);
            g.Box(V(x-.53f,1.06f,-2.15f),V(x+.53f,1.21f,-1.78f),14);
            g.Blob(V(x,1.2f,-2.11f),V(.43f,.12f,.16f),29);
        }
        // Gable infill and rows of stepped overlapping terracotta tiles.
        for(int row=0;row<20;row++)
        {
            float x=-2.5f+row*.25f,y=3.05f+(2.5f-Mathf.Abs(x+.125f))*.83f;
            if(Mathf.Abs(x)<2.1f){g.Box(V(x,3,-1.78f),V(x+.26f,y, -1.6f),17);g.Box(V(x,3,1.7f),V(x+.26f,y,1.9f),17);}
            for(int tile=0;tile<11;tile++)g.Box(V(x,y,-2.25f+tile*.42f),V(x+.28f,y+.13f,-1.84f+tile*.42f),(byte)(19+(tile/2+row/3)%3));
        }
        g.Box(V(-.13f,5.07f,-2.33f),V(.13f,5.3f,2.42f),20);
        g.Branch(V(-2.54f,3,-2.32f),V(0,5.18f,-2.32f),.11f,.11f,13);g.Branch(V(0,5.18f,-2.32f),V(2.54f,3,-2.32f),.11f,.11f,13);
        g.Box(V(.95f,4, .6f),V(1.55f,5.4f,1.2f),11);g.Box(V(.85f,5.3f,.5f),V(1.65f,5.52f,1.3f),12);g.Box(V(1.06f,5.5f,.71f),V(1.44f,5.55f,1.09f),13);
        g.Box(V(-.72f,0,-2.65f),V(.72f,.15f,-2),11);return g;
    }
    internal static EnchantedGroveVoxels Island()
    {
        var g=new EnchantedGroveVoxels(.3f);
        for(int x=-33;x<=33;x++)for(int z=-28;z<=28;z++)
        {
            float px=(x+.5f)*.3f,pz=(z+.5f)*.3f;
            float edge=Mathf.Pow(Mathf.Abs(px)/9.9f,6)+Mathf.Pow(Mathf.Abs(pz)/8.4f,6);
            if(edge>1+EnchantedGroveVoxels.Noise(px,pz,0)*.06f)continue;
            bool pond=(px+2)*(px+2)/15+(pz+1)*(pz+1)/9<1;
            int top=pond?-2:-1;
            for(int y=-15;y<=top;y++)
            {
                // Layered rock with patches, no random single-voxel speckling.
                byte color=(byte)(10+Mathf.Clamp(Mathf.FloorToInt(EnchantedGroveVoxels.Noise(px*.7f,y*.3f,pz*.7f)*3),0,2));
                if(y==top)color=pond?(byte)28:(byte)(29+Mathf.Clamp(Mathf.FloorToInt(EnchantedGroveVoxels.Noise(px*.6f,0,pz*.6f)*3),0,2));
                g.Cells[new Vector3Int(x,y,z)]=color;
            }
        }
        return g;
    }
}
