using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    public static class BuffHud
    {
        const float CardWidth=184,CardHeight=44,Gap=6;
        static GUIStyle title,subtitle;
        public static string Name(BuffKind kind)
        {
            switch(kind){case BuffKind.Shield:return "ESCUDO";case BuffKind.Damage:return "DAÑO";case BuffKind.Defense:return "DEFENSA";default:return "VELOCIDAD";}
        }
        static Color Tint(BuffKind kind)=>BuffPresentation.Current?.For(kind)?.color??Color.white;
        static void Styles()
        {
            if(title!=null)return;
            title=new GUIStyle{font=QuietFantasyUI.Body,fontSize=14,alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip};
            subtitle=new GUIStyle(title){fontSize=12};
        }
        public static void DrawSkill(Rect rect,AbilityDefinition ability,IReadOnlyList<BuffView> views)
        {
            if(views==null)return;
            foreach(var view in views)
            {
                if(view.ability!=ability)continue;
                Color color=Tint(view.kind);
                if(view.ready)FantasyUI.Frame(new Rect(rect.x+2,rect.y+2,rect.width-4,rect.height-4),color);
                if(view.goal>0)
                {
                    float span=view.goal*10+(view.goal-1)*4,start=rect.center.x-span*.5f;
                    for(int i=0;i<view.goal;i++)PlayerHUD.Fill(new Rect(start+i*14,rect.yMax-13,10,4),i<view.progress?color:new Color(.22f,.25f,.27f));
                }
                GUI.Label(rect,new GUIContent("",view.label+" · "+view.detail),GUIStyle.none);
            }
        }
        // Follow the health/stamina group, independent of the skill bar's position or orientation.
        public static Rect Layout(Rect vitals,Vector2 viewport,int count)
        {
            float x=vitals.xMax+12,y=vitals.yMin;
            int columns=Mathf.Min(4,Mathf.FloorToInt((viewport.x-10-x+Gap)/(CardWidth+Gap)));
            if(columns<1)
            {
                x=Mathf.Clamp(vitals.xMin,10,Mathf.Max(10,viewport.x-CardWidth-10));y=vitals.yMax+10;
                columns=Mathf.Max(1,Mathf.FloorToInt((viewport.x-10-x+Gap)/(CardWidth+Gap)));
            }
            columns=Mathf.Clamp(columns,1,Mathf.Clamp(count,1,4));
            int rows=Mathf.CeilToInt(count/(float)columns);
            float width=columns*(CardWidth+Gap)-Gap,height=rows*(CardHeight+Gap)-Gap;
            return new Rect(Mathf.Clamp(x,10,Mathf.Max(10,viewport.x-width-10)),Mathf.Clamp(y,10,Mathf.Max(10,viewport.y-height-10)),width,height);
        }
        public static void Draw(IReadOnlyList<BuffView> views,Rect vitals,Vector2 viewport)
        {
            if(views==null)return;
            int total=0;foreach(var view in views)if(view.ready)total++;
            if(total==0)return;
            Styles();int shown=Mathf.Min(8,total),index=0;
            Rect area=Layout(vitals,viewport,shown);
            int columns=Mathf.Max(1,Mathf.RoundToInt((area.width+Gap)/(CardWidth+Gap)));
            foreach(var view in views)
            {
                if(!view.ready)continue;if(index>=shown)break;
                var rect=new Rect(area.x+index%columns*(CardWidth+Gap),area.y+index/columns*(CardHeight+Gap),CardWidth,CardHeight);
                Color color=Tint(view.kind);FantasyUI.Panel(rect,.86f);
                var icon=BuffPresentation.Current?.For(view.kind)?.icon;
                if(icon!=null)QuietFantasyUI.DrawIcon(new Rect(rect.x+7,rect.y+10,24,24),icon,color);
                title.normal.textColor=color;subtitle.normal.textColor=QuietFantasyUI.Ink;
                GUI.Label(new Rect(rect.x+36,rect.y+3,rect.width-42,19),view.label,title);
                string detail=view.remaining>=0?Mathf.CeilToInt(view.remaining)+" s":view.detail??"ACTIVO";
                if(view.remaining>=0&&!string.IsNullOrEmpty(view.detail))
                    detail=(view.kind==BuffKind.Speed?view.progress+"/"+view.goal+" · ":view.kind==BuffKind.Damage?"LISTO · ":"")+detail;
                if(index==shown-1&&total>shown)detail+=" · +"+(total-shown);
                GUI.Label(new Rect(rect.x+36,rect.y+21,rect.width-42,17),detail,subtitle);
                if(view.remaining>=0&&view.duration>0)PlayerHUD.Fill(new Rect(rect.x+5,rect.yMax-3,(rect.width-10)*Mathf.Clamp01(view.remaining/view.duration),2),color);
                GUI.Label(rect,new GUIContent("",view.label+" · "+view.detail+(view.remaining>=0?" · "+Mathf.CeilToInt(view.remaining)+" s":"")),GUIStyle.none);
                index++;
            }
        }
    }
}
