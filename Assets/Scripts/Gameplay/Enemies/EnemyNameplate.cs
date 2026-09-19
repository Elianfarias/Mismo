using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyNameplate : MonoBehaviour
    {
        private Health health;
        private GoblinController goblin;
        private BossController boss;
        private DragonBossController dragon;
        private bool elite;
        private void Start()
        {
            health=GetComponent<Health>();goblin=GetComponent<GoblinController>();boss=GetComponent<BossController>();elite=GetComponent<GoblinEliteVisual>()!=null;
            dragon=GetComponent<DragonBossController>();
            GetComponent<GoblinPresentation>()?.HideLegacyStatus();
            GetComponent<BossPresentation>()?.HideLegacyStatus();
        }
        private void OnGUI()
        {
            if (Mismo.Gameplay.Player.Equipment.Inventory.InventoryPanel.AnyOpen) return;
            var hud=PlayerHUD.Active;var camera=Camera.main;
            if(hud==null || camera==null || health==null || health.IsDead)return;
            float distance=Vector3.Distance(hud.transform.position,transform.position);
            if(distance>28)return;
            float headHeight=goblin?.Settings is CreatureSettings?(GetComponent<CapsuleCollider>()?.height??2)+.2f:2.05f*transform.lossyScale.y;
            Vector3 head=transform.position+Vector3.up*headHeight;
            Vector3 screen=camera.WorldToScreenPoint(head);
            if(screen.z<=0 || screen.x<0 || screen.x>Screen.width || screen.y<0 || screen.y>Screen.height)return;
            if(Physics.Linecast(camera.transform.position,head,out var hit,~(1<<2),QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(hud.transform))return;
            float scale=PlayerHUD.Scale;Matrix4x4 old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            bool isBoss=boss!=null||dragon!=null||goblin?.Settings?.isBoss==true;
            float width=isBoss?440:180;
            float x=isBoss?(Screen.width/scale-width)/2:screen.x/scale-width/2;
            float y=isBoss?26:(Screen.height-screen.y)/scale-40;
            string name=boss!=null?"GUARDIÁN DEL SANTUARIO":elite?"GOBLIN ÉLITE":"GOBLIN";
            if(goblin?.Settings is CreatureSettings)name=goblin.Settings.displayName;
            if(dragon!=null&&dragon.Settings!=null)name=dragon.Settings.displayName+(dragon.Enraged?" · FURIA":"");
            var identity=GetComponent<Mismo.Gameplay.Player.World.WorldEnemyIdentity>();
            if(identity!=null)name=(goblin?.Settings is CreatureSettings?goblin.Settings.displayName:identity.DisplayName)+" · Nv "+identity.Level;
            bool warning=boss!=null?boss.State==BossState.Telegraph:goblin!=null&&goblin.State==GoblinState.Telegraph;
            bool attack=boss!=null?boss.State==BossState.Attack:goblin!=null&&goblin.State==GoblinState.Attack;
            if(dragon!=null){warning=dragon.Telegraphing;attack=dragon.State==DragonBossState.Attacking&&!warning;}
            Color accent=boss!=null?new Color(.85f,.35f,.32f):elite?new Color(.72f,.48f,.93f):new Color(.85f,.35f,.32f);
            if(dragon!=null&&dragon.Settings!=null)accent=dragon.Settings.accent;
            PlayerHUD.Fill(new Rect(x-6,y-4,width+12,isBoss?65:49),PlayerHUD.Panel);
            hud.Label(new Rect(x,y,width,20),name,isBoss?16:12,elite?new Color(.84f,.65f,1):PlayerHUD.Gold,TextAnchor.MiddleCenter);
            PlayerHUD.Fill(new Rect(x,y+25,width,7),new Color(.18f,.20f,.23f));
            PlayerHUD.Fill(new Rect(x,y+25,width*health.Normalized,7),accent);
            var posture=GetComponent<CombatState>();
            if(posture!=null&&posture.UsesPosture)
            {
                PlayerHUD.Fill(new Rect(x,y+35,width,5),new Color(.18f,.2f,.23f));
                PlayerHUD.Fill(new Rect(x,y+35,width*posture.PostureNormalized,5),PlayerHUD.Gold);
                if(posture.Broken)hud.Label(new Rect(x-20,y+72,width+40,20),"¡POSTURA ROTA!",13,PlayerHUD.Gold,TextAnchor.MiddleCenter);
            }
            if(isBoss)hud.Label(new Rect(x,y+43,width,20),health.Current.ToString("0")+" / "+health.Maximum.ToString("0"),12,Color.white,TextAnchor.MiddleCenter);
            if(warning||attack)hud.Label(new Rect(x-20,y+52,width+40,20),attack?"¡ATAQUE!":"PREPARANDO ATAQUE",12,attack?new Color(1,.4f,.3f):PlayerHUD.Gold,TextAnchor.MiddleCenter);
            GUI.matrix=old;
        }
    }
}
