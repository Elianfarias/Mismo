using System;
using System.Collections;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Dash;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Integración real en Play Mode; usa la arena y el prefab, sin guardar cambios de prueba.</summary>
    public static class GoblinPlayModeChecks
    {
        private const string Pending = "Mismo.GoblinChecks.Pending";
        private static IEnumerator routine;
        private static int frame = -1;
        private static double deadline;
        private static PlayerController player;
        private static Health playerHealth;
        private static GoblinController goblin;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void DisableHeadlessHud()
        {
            if(!Application.isBatchMode||!SessionState.GetBool(Pending,false))return;
            foreach(var hud in Object.FindObjectsByType<Mismo.Gameplay.Player.Presentation.PlayerHUD>(FindObjectsSortMode.None))hud.enabled=false;
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode) return;
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            GoblinPrototypeTool.Build();
            EditorSceneManager.OpenScene(GoblinPrototypeTool.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!Application.isBatchMode || !SessionState.GetBool(Pending, false)) return;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Step;
        }

        private static void Step()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout de Play Mode"); return; }
            if (!EditorApplication.isPlaying || !Application.isPlaying || Time.frameCount < 3 || frame == Time.frameCount) return;
            frame = Time.frameCount;
            try
            {
                if (routine == null) routine = Checks();
                if (!routine.MoveNext()) Finish(true, "detección, persecución, golpe único, carga, esquiva, parry, dash, stagger, muerte y pérdida de objetivo");
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static IEnumerator Checks()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            player = Object.FindFirstObjectByType<PlayerController>();
            Require(player != null, "Jugador de la arena");
            player.enabled = false; // El test controla posición y habilidades sin input de escritorio.
            playerHealth = player.GetComponent<Health>();
            Require(playerHealth != null && player.GetComponent<DamageReceiver>() != null, "Jugador recibe daño");
            // The editable arena can contain a boss; isolate this goblin regression from other AI.
            foreach(var boss in Object.FindObjectsByType<BossController>(FindObjectsSortMode.None))Object.DestroyImmediate(boss.gameObject);
            foreach (GameObject root in player.gameObject.scene.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, "Sin scripts perdidos: " + item.name);

            Reset(new Vector3(0, 0, 1.65f), Vector3.zero);
            yield return null;
            float until = Time.time + 4f;
            while (goblin.State != GoblinState.Telegraph && Time.time < until) yield return null;
            Require(goblin.State == GoblinState.Telegraph && goblin.CurrentAttack == goblin.Settings.slash, "Golpe anunciado");
            var animation=goblin.GetComponent<GoblinAnimationDriver>();
            Require(animation!=null && animation.Animator!=null && !animation.Animator.applyRootMotion,"Concept rig and animation driver");
            yield return null;yield return null;
            Require(animation.Animator.GetInteger("Motion")==3,"Telegraph uses authored attack");
            foreach(var clip in animation.Animator.runtimeAnimatorController.animationClips)
                Require(!clip.name.StartsWith("__preview"),"Runtime clips, not previews");
            Require(Mathf.Approximately(playerHealth.Current, playerHealth.Maximum), "Telegraph sin daño");
            until = Time.time + 3;
            while (goblin.State != GoblinState.Recovery && Time.time < until) yield return null;
            Require(goblin.State == GoblinState.Recovery, "Recuperación del golpe");
            Require(Mathf.Approximately(playerHealth.Current, playerHealth.Maximum - goblin.Settings.slash.damage), "Un solo impacto del golpe");
            Debug.Log("GOBLIN PASS: golpe anunciado, daño único y recuperación");

            Reset(new Vector3(0, 0, 1.65f), Vector3.zero);
            yield return null;
            until = Time.time + 4;
            while (goblin.State != GoblinState.Telegraph && Time.time < until) yield return null;
            while (goblin.State == GoblinState.Telegraph && goblin.StateProgress < 0.9f && Time.time < until) yield return null;
            SwordParry parry = player.GetComponentInChildren<SwordParry>();
            Require(parry != null, "Espada con parry");
            parry.Tick(10f); Require(parry.RequestParry(), "Parry disponible");
            until = Time.time + 2;
            while (goblin.State == GoblinState.Telegraph && Time.time < until) yield return null;
            Require(goblin.State == GoblinState.Stagger, "Parry interrumpe al goblin");
            Require(Mathf.Approximately(playerHealth.Current, playerHealth.Maximum), "Parry evita daño");
            float staggerUntil = Time.time + 0.3f;
            while (Time.time < staggerUntil) yield return null;
            Require(goblin.State == GoblinState.Stagger && Mathf.Approximately(playerHealth.Current, playerHealth.Maximum), "No queda daño pendiente tras parry");
            Debug.Log("GOBLIN PASS: parry cancela e interrumpe el ataque");

            Reset(new Vector3(0, 0, 1.65f), Vector3.zero);
            yield return null;
            until = Time.time + 4;
            while (goblin.State != GoblinState.Telegraph && Time.time < until) yield return null;
            while (goblin.State == GoblinState.Telegraph && goblin.StateProgress < 0.95f && Time.time < until) yield return null;
            BeltDash dash = player.GetComponent<BeltDash>();
            dash.Cancel(); dash.TickCooldown(10);
            Require(dash.TryStart(Vector3.right, true), "Dash disponible");
            Require(player.GetComponent<Invulnerability>().IsInvulnerable, "Dash abre iFrames");
            until = Time.time + 2;
            while (goblin.State != GoblinState.Recovery && Time.time < until) yield return null;
            Require(Mathf.Approximately(playerHealth.Current, playerHealth.Maximum), "iFrames evitan todo el golpe, sin reimpactar al expirar");
            dash.Cancel();
            Debug.Log("GOBLIN PASS: dash e impacto rechazado no se repite");

            Reset(new Vector3(0, 0, 4.2f), Vector3.zero);
            yield return null;
            until = Time.time + 4;
            while (goblin.State != GoblinState.Telegraph && Time.time < until) yield return null;
            Require(goblin.CurrentAttack == goblin.Settings.charge, "Carga a media distancia");
            Vector3 direction = goblin.transform.forward;
            PlacePlayer(new Vector3(3f, 0, 4.2f));
            until = Time.time + 3;
            while (goblin.State != GoblinState.Recovery && Time.time < until) yield return null;
            Require(goblin.State == GoblinState.Recovery, "Carga completa");
            Require(Vector3.Angle(direction, goblin.transform.forward) < 1f, "Carga no gira siguiendo al jugador");
            Require(goblin.transform.position.z > 2f, "Carga desplaza al goblin");
            Require(Mathf.Approximately(playerHealth.Current, playerHealth.Maximum), "Salir de la trayectoria esquiva la carga");
            Debug.Log("GOBLIN PASS: carga telegrafiada y esquiva lateral");

            Reset(new Vector3(0, 0, 4.2f), Vector3.zero);
            yield return null;
            until = Time.time + 5;
            while (goblin.State != GoblinState.Recovery && Time.time < until) yield return null;
            Require(Mathf.Approximately(playerHealth.Current, playerHealth.Maximum - goblin.Settings.charge.damage), "Carga hace un solo impacto: health="+playerHealth.Current+" state="+goblin.State+" attack="+goblin.CurrentAttack?.label+" position="+goblin.transform.position+" player="+player.transform.position);

            Reset(new Vector3(0, 0, 4.2f), Vector3.zero);
            yield return null;
            until = Time.time + 4;
            while (goblin.State != GoblinState.Telegraph && Time.time < until) yield return null;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 1, 1.8f); wall.transform.localScale = new Vector3(3, 2, 0.5f);
            Physics.SyncTransforms();
            until = Time.time + 3;
            while (goblin.State != GoblinState.Recovery && Time.time < until) yield return null;
            Require(goblin.transform.position.z < 1.3f && Mathf.Approximately(playerHealth.Current, playerHealth.Maximum), "Carga no atraviesa pared ni daña detrás");
            Object.DestroyImmediate(wall);
            Debug.Log("GOBLIN PASS: carga hace daño único y respeta paredes");

            Reset(new Vector3(0, 0, 1.65f), Vector3.zero);
            yield return null;
            until = Time.time + 4;
            while (goblin.State != GoblinState.Telegraph && Time.time < until) yield return null;
            var receiver = goblin.GetComponent<DamageReceiver>();
            Require(receiver.ReceiveDamage(new DamageInfo(18, player.gameObject, goblin.transform.position, Vector3.forward)), "Goblin recibe daño real");
            Require(goblin.State == GoblinState.Stagger, "Golpe fuerte cancela telegraph");
            yield return null;yield return null;
            Require(goblin.GetComponent<GoblinAnimationDriver>().Animator.GetInteger("Motion")==4,"Damage plays Hit");
            receiver.ReceiveDamage(new DamageInfo(1000, player.gameObject, goblin.transform.position, Vector3.forward));
            Require(goblin.State == GoblinState.Dead && !goblin.GetComponent<NavMeshAgent>().enabled, "Muerte cancela IA");
            foreach (Collider collider in goblin.GetComponentsInChildren<Collider>()) Require(!collider.enabled, "Muerte desactiva collider");
            until = Time.time + 0.5f;
            while (Time.time < until) yield return null;
            Require(Mathf.Approximately(playerHealth.Current, playerHealth.Maximum), "Muerto no hace daño");
            Debug.Log("GOBLIN PASS: daño, stagger y muerte durante ataque");

            Reset(new Vector3(0, 0, 30), Vector3.zero);
            yield return null;
            Require(goblin.Target == null, "Distant player is initially undetected");
            var distantHit = new DamageInfo(1, player.gameObject, goblin.transform.position,
                Vector3.back, AttackIdentity.Next(), 0, true);
            Require(goblin.GetComponent<DamageReceiver>().ReceiveDamage(distantHit), "Distant arrow deals damage");
            Require(goblin.Target == player.transform && goblin.State == GoblinState.Chase,
                "Damage acquires the attacker beyond detection and loss ranges");
            goblin.Tick(.1f);
            Require(goblin.Target == player.transform && goblin.State == GoblinState.Chase,
                "Provoked target survives the next distance validation");
            PlacePlayer(new Vector3(0, 0, 200));
            goblin.Tick(.1f);
            Require(goblin.Target == null, "Provoked pursuit still has an escape limit");

            Reset(new Vector3(0, 0, 10), Vector3.zero);
            yield return null;
            Vector3 before = goblin.transform.position;
            until = Time.time + 0.6f;
            while (Time.time < until) yield return null;
            Require(goblin.Target == player.transform && goblin.transform.position.z > before.z + 0.2f, "Detección y persecución");
            Require(goblin.GetComponent<GoblinAnimationDriver>().Animator.GetInteger("Motion")==2,"Chase plays Run");
            PlacePlayer(new Vector3(0, 0, 30));
            yield return null; yield return null;
            Require(goblin.State == GoblinState.Return || goblin.State == GoblinState.Idle, "Pierde objetivo lejano y vuelve");

            Reset(new Vector3(-6, 0, 3), new Vector3(-2, 0, 3));
            yield return null;
            until = Time.time + 0.8f;
            while (Time.time < until) yield return null;
            Require(goblin.Target == null && goblin.State == GoblinState.Idle, "Pared bloquea detección");
            var path = new NavMeshPath();
            Require(NavMesh.CalculatePath(new Vector3(-2, 0, 3), new Vector3(-6, 0, 3), NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete && path.corners.Length > 2, "NavMesh rodea el obstáculo");

            Reset(new Vector3(0, 0, 1.65f), Vector3.zero);
            yield return null;
            playerHealth.ApplyDamage(new DamageInfo(1000, goblin.gameObject, player.transform.position, Vector3.forward));
            yield return null; yield return null;
            Require(goblin.State == GoblinState.Return || goblin.State == GoblinState.Idle, "Deja de atacar al jugador muerto");
        }

        private static void Reset(Vector3 playerPosition, Vector3 goblinPosition)
        {
            foreach (GoblinController existing in Object.FindObjectsByType<GoblinController>(FindObjectsSortMode.None)) Object.DestroyImmediate(existing.gameObject);
            playerHealth.Revive(); player.GetComponent<Invulnerability>().Cancel();
            player.GetComponentInChildren<SwordParry>()?.Tick(100f);
            PlacePlayer(playerPosition);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPrototypeTool.PrefabPath);
            goblin = Object.Instantiate(prefab, goblinPosition, Quaternion.identity).GetComponent<GoblinController>();
        }
        private static void PlacePlayer(Vector3 position)
        {
            CharacterController body = player.GetComponent<CharacterController>();
            body.enabled = false; player.transform.position = position; body.enabled = true;
            Physics.SyncTransforms();
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Step; SessionState.SetBool(Pending, false);
            Debug.Log((success ? "GOBLIN CHECKS PASSED: " : "GOBLIN CHECKS FAILED: ") + message);
            System.IO.Directory.CreateDirectory("Docs/Validation");
            System.IO.File.WriteAllText("Docs/Validation/GoblinConceptIntegration.txt",(success?"PASS ":"FAIL ")+message+"; concept rig, runtime clips, attack synchronization, Hit and Run");
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
