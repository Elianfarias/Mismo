using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEngine;
namespace Mismo.Gameplay.Player.Editor
{
    public sealed partial class WeaponPoseWindow
    {
        bool editTrailEndpoints,previewTrail=true,playingTrail,impactOnly=true;
        int trailEndpoint,previewComboStep;
        double playbackClock;
        WeaponTrailRibbon previewRibbon;
        Material previewTrailMaterial;
        void DrawTrailGUI()
        {
            EditorGUILayout.Space();EditorGUILayout.LabelField("Trail procedural · melee",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Marcá la base y la punta de la zona de hoja que deja estela. Los puntos pertenecen al visual del arma. Los golpes con Shape = Blade también los usan para detectar el contacto de la hoja. Los cambios se guardan en el perfil compartido.",MessageType.Info);
            var data=new SerializedObject(profile);data.Update();EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(data.FindProperty("meleeTrail"),new GUIContent("Activar estela"));
            EditorGUILayout.PropertyField(data.FindProperty("proceduralTrail"),new GUIContent("Cinta procedural"));
            if(editSecond)EditorGUILayout.PropertyField(data.FindProperty("secondaryTrail"),new GUIContent("Estela en segunda pieza"));
            EditorGUILayout.PropertyField(data.FindProperty(editSecond?"secondaryTrailBase":"trailBase"),new GUIContent("Base de la hoja"));
            EditorGUILayout.PropertyField(data.FindProperty(editSecond?"secondaryTrailTip":"trailTip"),new GUIContent("Punta de la hoja"));
            EditorGUILayout.PropertyField(data.FindProperty("trailDuration"),new GUIContent("Persistencia (segundos)"));
            EditorGUILayout.PropertyField(data.FindProperty("trailTaper"),new GUIContent("Estrechar la cola"));
            EditorGUILayout.PropertyField(data.FindProperty("trailStartColor"),new GUIContent("Color nuevo"));
            EditorGUILayout.PropertyField(data.FindProperty("trailEndColor"),new GUIContent("Color al desaparecer"));
            EditorGUILayout.PropertyField(data.FindProperty("trailMaterial"),new GUIContent("Material opcional"));
            if(EditorGUI.EndChangeCheck()){data.ApplyModifiedProperties();Refresh();}
            editTrailEndpoints=EditorGUILayout.Toggle("Editar puntos en Scene",editTrailEndpoints);
            if(editTrailEndpoints){editTrailTip=false;trailEndpoint=GUILayout.Toolbar(trailEndpoint,new[]{"Base","Punta"});}
            using(new EditorGUI.DisabledScope(SelectedVisual==null))
                if(GUILayout.Button("Ajustar puntos a los límites de la pieza"))FitTrailEndpoints();
            EditorGUI.BeginChangeCheck();previewTrail=EditorGUILayout.Toggle("Mostrar previsualización",previewTrail);impactOnly=EditorGUILayout.Toggle("Solo ventana de impacto",impactOnly);
            var ability=weapon.GetAbility(AbilitySlot.Basic);
            if(ability!=null&&ability.comboSteps!=null&&ability.comboSteps.Length>0)
                previewComboStep=EditorGUILayout.IntSlider("Golpe del combo",previewComboStep+1,1,ability.comboSteps.Length)-1;
            if(EditorGUI.EndChangeCheck())Refresh();
            if(ability!=null&&ability.comboSteps!=null&&previewComboStep>=0&&previewComboStep<ability.comboSteps.Length)
            {
                var abilityData=new SerializedObject(ability);abilityData.Update();
                var selectedStep=abilityData.FindProperty("comboSteps").GetArrayElementAtIndex(previewComboStep);
                var start=selectedStep.FindPropertyRelative("impactStartPercent");
                var end=selectedStep.FindPropertyRelative("impactEndPercent");
                EditorGUILayout.LabelField("Ventana del golpe "+(previewComboStep+1),EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Estos porcentajes se guardan en este golpe de la habilidad. Controlan tanto la estela como el daño; cada golpe conserva sus propios valores. Activá Solo ventana de impacto para previsualizarlos.",MessageType.Info);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(selectedStep.FindPropertyRelative("shape"),new GUIContent("Detección del golpe"));
                if(selectedStep.FindPropertyRelative("shape").enumValueIndex==2)
                {
                    EditorGUILayout.PropertyField(selectedStep.FindPropertyRelative("bladeRadius"),new GUIContent("Radio de contacto de hoja (m)"));
                    EditorGUILayout.HelpBox("Blade detecta contacto a lo largo de Base y Punta, después de posicionar el arma. No usa la caja frontal. Ajustá los extremos para cubrir solo la hoja.",MessageType.Info);
                }
                float from=EditorGUILayout.Slider("Inicio de impacto / estela (%)",start.floatValue,0,100);
                float to=EditorGUILayout.Slider("Fin de impacto / estela (%)",end.floatValue,from,100);
                if(EditorGUI.EndChangeCheck())
                {
                    start.floatValue=from;end.floatValue=Mathf.Max(from,to);
                    abilityData.ApplyModifiedProperties();Refresh();
                }
                if(end.floatValue<=start.floatValue)EditorGUILayout.HelpBox("La ventana está vacía: este golpe no emitirá estela ni hará daño.",MessageType.Warning);
                if(GUILayout.Button("Guardar porcentajes de la habilidad"))AssetDatabase.SaveAssetIfDirty(ability);
            }
            if(GUILayout.Button("Usar clip del golpe seleccionado"))
            {
                var binding=weapon.family!=null&&weapon.family.animations!=null?weapon.family.animations.Find(ability):null;
                if(binding!=null&&binding.comboClips!=null&&previewComboStep<binding.comboClips.Length){clip=binding.comboClips[previewComboStep];time=0;Refresh();}
            }
            using(new EditorGUI.DisabledScope(stage==null||clip==null))
                if(GUILayout.Button(playingTrail?"Pausar previsualización":"Reproducir golpe en bucle")){playingTrail=!playingTrail;playbackClock=EditorApplication.timeSinceStartup;}
            if(GUILayout.Button("Guardar trail del perfil"))AssetDatabase.SaveAssetIfDirty(profile);
        }
        void UpdateTrailPlayback()
        {
            if(!playingTrail||stage==null||clip==null||EditorApplication.isPlaying)return;
            double now=EditorApplication.timeSinceStartup;float dt=Mathf.Min(.05f,(float)(now-playbackClock));playbackClock=now;
            float duration=PreviewDuration();time=Mathf.Repeat(time+dt*clip.length/duration,Mathf.Max(.001f,clip.length));Refresh();
        }
        float PreviewDuration()
        {
            var ability=weapon!=null?weapon.GetAbility(AbilitySlot.Basic):null;
            return ability!=null&&ability.comboSteps!=null&&previewComboStep<ability.comboSteps.Length&&ability.comboSteps[previewComboStep]!=null?ability.comboSteps[previewComboStep].Duration:Mathf.Max(.001f,clip!=null?clip.length:1);
        }
        void SampleTrailPose(float at)
        {
            for(int i=0;i<previewBones.Length;i++){previewBones[i].localPosition=bonePositions[i];previewBones[i].localRotation=boneRotations[i];previewBones[i].localScale=boneScales[i];}
            if(clip!=null&&animator!=null)clip.SampleAnimation(animator.gameObject,at);
            ApplyPreviewPose(visual,holstered?profile.holstered:profile.equipped);
            if(secondVisual!=null)ApplyPreviewPose(secondVisual,holstered?weapon.secondaryHolstered:weapon.secondaryEquipped);
        }
        void RefreshTrailPreview()
        {
            if(!previewTrail||!profile.proceduralTrail||!profile.meleeTrail||clip==null||holstered||SelectedVisual==null||editSecond&&!profile.secondaryTrail){previewRibbon?.Clear();return;}
            if(previewRibbon==null)
            {
                var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/CombatParticles.shader");
                if(shader==null)return;
                previewTrailMaterial=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                previewRibbon=new WeaponTrailRibbon(character.transform,previewTrailMaterial);
            }
            previewRibbon.Clear();float duration=PreviewDuration();float now=time/Mathf.Max(.001f,clip.length)*duration;
            float first=Mathf.Max(0,now-Mathf.Max(.01f,profile.trailDuration));
            var ability=weapon.GetAbility(AbilitySlot.Basic);
            var step=ability!=null&&ability.comboSteps!=null&&previewComboStep<ability.comboSteps.Length?ability.comboSteps[previewComboStep]:null;
            int count=Mathf.Clamp(Mathf.CeilToInt((now-first)*90),1,190);
            for(int i=0;i<=count;i++)
            {
                float t=Mathf.Lerp(first,now,(float)i/count);SampleTrailPose(t/duration*clip.length);
                bool emit=!impactOnly||step==null||t/duration>=step.ImpactStart&&t/duration<step.ImpactEnd;
                previewRibbon.SampleBlade(SelectedVisual.transform,profile,t,emit,editSecond);
            }
            SampleTrailPose(time);
        }
        void DisposeTrailPreview(){previewRibbon?.Dispose();previewRibbon=null;if(previewTrailMaterial!=null)DestroyImmediate(previewTrailMaterial);previewTrailMaterial=null;}
        void DrawTrailEndpoints()
        {
            var root=SelectedVisual.transform;Vector3 a=root.TransformPoint(editSecond?profile.secondaryTrailBase:profile.trailBase),b=root.TransformPoint(editSecond?profile.secondaryTrailTip:profile.trailTip);
            Handles.color=Color.cyan;Handles.DrawAAPolyLine(4,a,b);
            Handles.Label(a,"Base del trail");Handles.Label(b,"Punta del trail");
            if(Handles.Button(a,Quaternion.identity,HandleUtility.GetHandleSize(a)*.06f,HandleUtility.GetHandleSize(a)*.08f,Handles.SphereHandleCap))trailEndpoint=0;
            if(Handles.Button(b,Quaternion.identity,HandleUtility.GetHandleSize(b)*.06f,HandleUtility.GetHandleSize(b)*.08f,Handles.SphereHandleCap))trailEndpoint=1;
            EditorGUI.BeginChangeCheck();Vector3 point=Handles.PositionHandle(trailEndpoint==0?a:b,root.rotation);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile,"Mover extremo de trail");Vector3 local=root.InverseTransformPoint(point);
                if(editSecond){if(trailEndpoint==0)profile.secondaryTrailBase=local;else profile.secondaryTrailTip=local;}
                else{if(trailEndpoint==0)profile.trailBase=local;else profile.trailTip=local;}
                EditorUtility.SetDirty(profile);Refresh();
            }
        }
        void FitTrailEndpoints()
        {
            var root=SelectedVisual.transform;Bounds bounds=new Bounds();bool has=false;
            foreach(var renderer in SelectedVisual.GetComponentsInChildren<Renderer>())
            {
                var b=renderer.localBounds;
                for(int i=0;i<8;i++){Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));p=root.InverseTransformPoint(renderer.transform.TransformPoint(p));if(!has){bounds=new Bounds(p,Vector3.zero);has=true;}else bounds.Encapsulate(p);}
            }
            if(!has)return;int axis=bounds.size.x>bounds.size.y?0:1;if(bounds.size.z>bounds.size[axis])axis=2;
            Vector3 a=bounds.center,bTip=bounds.center;a[axis]=bounds.min[axis];bTip[axis]=bounds.max[axis];
            Undo.RecordObject(profile,"Ajustar trail a la pieza");profile.meleeTrail=profile.proceduralTrail=true;
            if(editSecond){profile.secondaryTrail=true;profile.secondaryTrailBase=a;profile.secondaryTrailTip=bTip;}else{profile.trailBase=a;profile.trailTip=bTip;}
            editTrailEndpoints=true;EditorUtility.SetDirty(profile);Refresh();
        }
    }
}
