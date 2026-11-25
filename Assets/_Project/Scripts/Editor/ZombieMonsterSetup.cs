using System.Collections.Generic;
using System.IO;
using Ogni.Entities.Listener;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Меню Ogni -> Setup Zombie Monster.
    /// Полная интеграция всех 12 анимаций из Scary Zombie Pack (Assets/_Project/Monsters/Zombie/):
    /// - Ch30_nonPBR.fbx — основная 3D-модель (Humanoid + URP материалы).
    /// - zombie idle / zombie walk / zombie run — покой, патруль, погоня.
    /// - zombie crawl / running crawl — ползание при низком HP (<= 35%).
    /// - zombie attack / zombie neck bite / zombie biting / zombie biting (2) — 4 случайных варианта атаки.
    /// - zombie scream — крик при обнаружении игрока и реакция на попадание (Hit / Scream).
    /// - zombie death / zombie dying — 2 случайных варианта смерти.
    /// </summary>
    public static class ZombieMonsterSetup
    {
        public const string ZombieFolder = "Assets/_Project/Monsters/Zombie";
        private const string TexturesFolder = ZombieFolder + "/Textures";
        private const string ControllerPath = ZombieFolder + "/ZombieAnimator.controller";
        public const string PrefabPath = "Assets/_Project/Prefabs/Zombie.prefab";

        private enum ZombieClipKind
        {
            None,
            Idle,
            Walk,
            Run,
            CrawlWalk,
            CrawlRun,
            Attack,
            Scream,
            Death
        }

        [MenuItem("Ogni/Setup Zombie Monster")]
        public static void BuildFromMenu()
        {
            GameObject prefab = BuildPrefab();
            if (prefab != null)
            {
                Debug.Log("[Ogni] Префаб зомби со всеми 12 анимациями готов: " + PrefabPath);
            }
        }

        public static GameObject BuildPrefab()
        {
            EnsureFolder("Assets/_Project/Monsters");
            EnsureFolder(ZombieFolder);
            EnsureFolder(TexturesFolder);
            EnsureFolder("Assets/_Project/Prefabs");

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ZombieFolder });
            if (guids.Length == 0)
            {
                Debug.LogWarning("[Ogni] В папке " + ZombieFolder + " не найдено FBX-файлов. Распакуйте туда Scary Zombie Pack.");
                return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }

            string modelPath = null;
            AnimationClip idleClip = null;
            AnimationClip walkClip = null;
            AnimationClip runClip = null;
            AnimationClip crawlWalkClip = null;
            AnimationClip crawlRunClip = null;
            AnimationClip screamClip = null;
            var attackClips = new List<AnimationClip>();
            var deathClips = new List<AnimationClip>();

            // 1. Находим основную модель с мешем (Ch30_nonPBR.fbx или самый крупный FBX).
            long maxSize = -1;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (fileName.Contains("ch30") || fileName.Contains("nonpbr"))
                {
                    modelPath = path;
                    break;
                }
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    long len = new FileInfo(fullPath).Length;
                    if (len > maxSize)
                    {
                        maxSize = len;
                        modelPath = path;
                    }
                }
            }

            // 2. Импортируем основную модель (Humanoid + извлечение текстур и материалов).
            if (!string.IsNullOrEmpty(modelPath))
            {
                ConfigureImporter(modelPath, extractMaterials: true, loopTime: true);
            }

            // 3. Импортируем все 12 FBX-анимаций и раскладываем их по категориям.
            // Сортируем пути для детерминированного порядка вариантов Attack_0..3 и Death_0..1.
            var sortedPaths = new List<string>(guids.Length);
            foreach (string g in guids) sortedPaths.Add(AssetDatabase.GUIDToAssetPath(g));
            sortedPaths.Sort(System.StringComparer.OrdinalIgnoreCase);

            foreach (string path in sortedPaths)
            {
                if (path == modelPath) continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                ZombieClipKind kind = Classify(fileName);
                if (kind == ZombieClipKind.None) continue;

                bool loop = kind == ZombieClipKind.Idle
                    || kind == ZombieClipKind.Walk
                    || kind == ZombieClipKind.Run
                    || kind == ZombieClipKind.CrawlWalk
                    || kind == ZombieClipKind.CrawlRun;

                ConfigureImporter(path, extractMaterials: false, loopTime: loop);

                foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(obj is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                    switch (kind)
                    {
                        case ZombieClipKind.Idle: if (idleClip == null) idleClip = clip; break;
                        case ZombieClipKind.Walk: if (walkClip == null) walkClip = clip; break;
                        case ZombieClipKind.Run: if (runClip == null) runClip = clip; break;
                        case ZombieClipKind.CrawlWalk: if (crawlWalkClip == null) crawlWalkClip = clip; break;
                        case ZombieClipKind.CrawlRun: if (crawlRunClip == null) crawlRunClip = clip; break;
                        case ZombieClipKind.Scream: if (screamClip == null) screamClip = clip; break;
                        case ZombieClipKind.Attack: attackClips.Add(clip); break;
                        case ZombieClipKind.Death: deathClips.Add(clip); break;
                    }
                }
            }

            FixZombieMaterials();

            // 4. Собираем AnimatorController со всеми состояниями и вариантами.
            AnimatorController controller = BuildAnimatorController(
                idleClip, walkClip, runClip, crawlWalkClip, crawlRunClip, screamClip, attackClips, deathClips);

            // 5. Собираем сетевой префаб Zombie.prefab.
            GameObject modelAsset = !string.IsNullOrEmpty(modelPath)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(modelPath)
                : null;
            if (modelAsset == null)
            {
                Debug.LogWarning("[Ogni] Не удалось загрузить модель зомби: " + modelPath);
                return null;
            }

            GameObject root = new GameObject("Zombie");
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            ListenerAI ai = root.AddComponent<ListenerAI>();

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.45f;
            collider.height = 1.9f;
            collider.center = new Vector3(0f, 0.95f, 0f);

            GameObject rig = new GameObject("VisualRig");
            rig.transform.SetParent(root.transform, false);

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            visual.name = "ZombieModel";
            visual.transform.SetParent(rig.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            foreach (Collider col in visual.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(col);
            }

            Animator anim = visual.GetComponent<Animator>();
            if (anim == null) anim = visual.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            // Автоматически подгоняем рост зомби к 1.92 м (независимо от UnitScaleFactor в FBX) и ставим стопами на y = 0.
            NormalizeZombieHeight(visual, anim, 1.92f);

            SerializedObject so = new SerializedObject(ai);
            SerializedProperty rigProp = so.FindProperty("visualRig");
            if (rigProp != null) rigProp.objectReferenceValue = rig.transform;
            SerializedProperty animProp = so.FindProperty("animator");
            if (animProp != null) animProp.objectReferenceValue = anim;
            SerializedProperty patrolProp = so.FindProperty("patrolRadius");
            if (patrolProp != null) patrolProp.floatValue = 22f;
            SerializedProperty atkCountProp = so.FindProperty("attackVariantCount");
            if (atkCountProp != null) atkCountProp.intValue = Mathf.Max(1, attackClips.Count);
            SerializedProperty deathCountProp = so.FindProperty("deathVariantCount");
            if (deathCountProp != null) deathCountProp.intValue = Mathf.Max(1, deathClips.Count);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[Ogni] Зомби настроен ({Path.GetFileName(modelPath)}): " +
                $"Idle={(idleClip ? "OK" : "-")}, Walk={(walkClip ? "OK" : "-")}, Run={(runClip ? "OK" : "-")}, " +
                $"CrawlWalk={(crawlWalkClip ? "OK" : "-")}, CrawlRun={(crawlRunClip ? "OK" : "-")}, " +
                $"Scream={(screamClip ? "OK" : "-")}, Attacks={attackClips.Count}, Deaths={deathClips.Count}.");

            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static ZombieClipKind Classify(string fileName)
        {
            string lower = fileName.ToLowerInvariant();
            if (lower.Contains("running crawl") || (lower.Contains("run") && lower.Contains("crawl"))) return ZombieClipKind.CrawlRun;
            if (lower.Contains("crawl")) return ZombieClipKind.CrawlWalk;
            if (lower.Contains("idle")) return ZombieClipKind.Idle;
            if (lower.Contains("walk")) return ZombieClipKind.Walk;
            if (lower.Contains("run")) return ZombieClipKind.Run;
            if (lower.Contains("biting")) return ZombieClipKind.None; // анимация поедания на полу, не для стоячей атаки
            if (lower.Contains("attack") || lower.Contains("neck bite")) return ZombieClipKind.Attack;
            if (lower.Contains("scream") || lower.Contains("roar") || lower.Contains("agony")) return ZombieClipKind.Scream;
            if (lower.Contains("death") || lower.Contains("dying")) return ZombieClipKind.Death;
            return ZombieClipKind.None;
        }

        private static void ConfigureImporter(string assetPath, bool extractMaterials, bool loopTime)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }
            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                dirty = true;
            }

            if (extractMaterials)
            {
                importer.ExtractTextures(TexturesFolder);
                if (importer.materialLocation != ModelImporterMaterialLocation.External)
                {
                    importer.materialLocation = ModelImporterMaterialLocation.External;
                    dirty = true;
                }
                if (importer.materialName != ModelImporterMaterialName.BasedOnMaterialName)
                {
                    importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                    dirty = true;
                }
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips != null && clips.Length > 0)
            {
                bool clipsDirty = importer.clipAnimations == null || importer.clipAnimations.Length == 0;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i].loopTime != loopTime) { clips[i].loopTime = loopTime; clipsDirty = true; }
                    if (!clips[i].lockRootRotation) { clips[i].lockRootRotation = true; clipsDirty = true; }
                    if (!clips[i].keepOriginalOrientation) { clips[i].keepOriginalOrientation = true; clipsDirty = true; }
                    if (!clips[i].lockRootHeightY) { clips[i].lockRootHeightY = true; clipsDirty = true; }
                    if (!clips[i].keepOriginalPositionY) { clips[i].keepOriginalPositionY = true; clipsDirty = true; }
                    if (clips[i].heightFromFeet) { clips[i].heightFromFeet = false; clipsDirty = true; }
                    if (clips[i].lockRootPositionXZ) { clips[i].lockRootPositionXZ = false; clipsDirty = true; }
                }

                if (clipsDirty)
                {
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
            }
        }

        private static void FixZombieMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { ZombieFolder }))
            {
                string matPath = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) continue;

                if (urpLit != null && mat.shader != urpLit)
                {
                    Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    Texture bump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                    mat.shader = urpLit;
                    if (mainTex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
                    if (bump != null && mat.HasProperty("_BumpMap"))
                    {
                        mat.SetTexture("_BumpMap", bump);
                        mat.EnableKeyword("_NORMALMAP");
                    }
                }

                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.14f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.14f);
                EditorUtility.SetDirty(mat);
            }
        }

        private static AnimatorController BuildAnimatorController(
            AnimationClip idleClip,
            AnimationClip walkClip,
            AnimationClip runClip,
            AnimationClip crawlWalkClip,
            AnimationClip crawlRunClip,
            AnimationClip screamClip,
            List<AnimationClip> attackClips,
            List<AnimationClip> deathClips)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsCrawling", AnimatorControllerParameterType.Bool);
            controller.AddParameter("AttackVariant", AnimatorControllerParameterType.Int);
            controller.AddParameter("DeathVariant", AnimatorControllerParameterType.Int);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Scream", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition anyTr in sm.anyStateTransitions)
            {
                sm.RemoveAnyStateTransition(anyTr);
            }
            foreach (ChildAnimatorState s in sm.states)
            {
                sm.RemoveState(s.state);
            }

            const float WalkThreshold = 0.15f;
            const float RunEnterThreshold = 2.25f;
            const float RunExitThreshold = 2.0f;
            const float CrawlRunThreshold = 1.6f;

            // --- Плавный 1D BlendTree (Idle -> Walk -> Run) по параметру Speed ---
            BlendTree locoTree;
            AnimatorState idleState = controller.CreateBlendTreeInController("Locomotion", out locoTree, 0);
            locoTree.blendType = BlendTreeType.Simple1D;
            locoTree.blendParameter = "Speed";
            locoTree.useAutomaticThresholds = false;
            if (idleClip != null) locoTree.AddChild(idleClip, 0.0f);
            if (walkClip != null) locoTree.AddChild(walkClip, 1.35f);
            if (runClip != null) locoTree.AddChild(runClip, 2.75f);
            sm.defaultState = idleState;
            AnimatorState walkState = null;
            AnimatorState runState = null;

            // --- Crawl (раненый зомби <= 35% HP) ---
            AnimatorState crawlWalkState = null;
            AnimatorState crawlRunState = null;
            if (crawlWalkClip != null)
            {
                crawlWalkState = sm.AddState("CrawlWalk", new Vector3(500f, 0f, 0f));
                crawlWalkState.motion = crawlWalkClip;

                AddCrawlEnter(idleState, crawlWalkState);
                if (walkState != null) AddCrawlEnter(walkState, crawlWalkState);
                if (runState != null) AddCrawlEnter(runState, crawlWalkState);

                AnimatorStateTransition exitCrawl = crawlWalkState.AddTransition(idleState);
                exitCrawl.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrawling");
                exitCrawl.hasExitTime = false; exitCrawl.duration = 0.2f;

                if (crawlRunClip != null)
                {
                    crawlRunState = sm.AddState("CrawlRun", new Vector3(760f, 0f, 0f));
                    crawlRunState.motion = crawlRunClip;

                    AnimatorStateTransition cwToCr = crawlWalkState.AddTransition(crawlRunState);
                    cwToCr.AddCondition(AnimatorConditionMode.Greater, CrawlRunThreshold, "Speed");
                    cwToCr.hasExitTime = false; cwToCr.duration = 0.15f;

                    AnimatorStateTransition crToCw = crawlRunState.AddTransition(crawlWalkState);
                    crToCw.AddCondition(AnimatorConditionMode.Less, CrawlRunThreshold - 0.2f, "Speed");
                    crToCw.hasExitTime = false; crToCw.duration = 0.15f;

                    AnimatorStateTransition exitCrawlRun = crawlRunState.AddTransition(idleState);
                    exitCrawlRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrawling");
                    exitCrawlRun.hasExitTime = false; exitCrawlRun.duration = 0.2f;
                }
            }

            // --- Attacks (все 4 варианта: attack, neck bite, biting, biting (2)) ---
            for (int i = 0; i < attackClips.Count; i++)
            {
                AnimatorState atkState = sm.AddState($"Attack_{i}", new Vector3(320f + i * 190f, 250f, 0f));
                atkState.motion = attackClips[i];

                AnimatorStateTransition anyToAtk = sm.AddAnyStateTransition(atkState);
                anyToAtk.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                anyToAtk.AddCondition(AnimatorConditionMode.Equals, i, "AttackVariant");
                anyToAtk.canTransitionToSelf = false;
                anyToAtk.hasExitTime = false;
                anyToAtk.duration = 0.22f;

                if (crawlWalkState != null)
                {
                    AnimatorStateTransition tr = atkState.AddTransition(crawlWalkState);
                    tr.AddCondition(AnimatorConditionMode.If, 0f, "IsCrawling");
                    tr.hasExitTime = true; tr.exitTime = 0.88f; tr.duration = 0.15f;
                }
                if (runState != null)
                {
                    AnimatorStateTransition tr = atkState.AddTransition(runState);
                    tr.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrawling");
                    tr.AddCondition(AnimatorConditionMode.Greater, RunEnterThreshold, "Speed");
                    tr.hasExitTime = true; tr.exitTime = 0.88f; tr.duration = 0.15f;
                }
                if (walkState != null)
                {
                    AnimatorStateTransition tr = atkState.AddTransition(walkState);
                    tr.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrawling");
                    tr.AddCondition(AnimatorConditionMode.Greater, WalkThreshold, "Speed");
                    tr.hasExitTime = true; tr.exitTime = 0.88f; tr.duration = 0.15f;
                }
                AnimatorStateTransition toIdle = atkState.AddTransition(idleState);
                toIdle.hasExitTime = true; toIdle.exitTime = 0.88f; toIdle.duration = 0.26f;
            }

            // --- Scream / Hit (крик при обнаружении игрока или попадании) ---
            if (screamClip != null)
            {
                AnimatorState screamState = sm.AddState("Scream", new Vector3(100f, 250f, 0f));
                screamState.motion = screamClip;

                AnimatorStateTransition anyToScream = sm.AddAnyStateTransition(screamState);
                anyToScream.AddCondition(AnimatorConditionMode.If, 0f, "Scream");
                anyToScream.canTransitionToSelf = false;
                anyToScream.hasExitTime = false;
                anyToScream.duration = 0.10f;

                AnimatorStateTransition anyToHit = sm.AddAnyStateTransition(screamState);
                anyToHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                anyToHit.canTransitionToSelf = false;
                anyToHit.hasExitTime = false;
                anyToHit.duration = 0.08f;

                AnimatorStateTransition toIdle = screamState.AddTransition(idleState);
                toIdle.hasExitTime = true; toIdle.exitTime = 0.85f; toIdle.duration = 0.26f;
            }

            // --- Deaths (2 варианта: zombie death и zombie dying) ---
            for (int i = 0; i < deathClips.Count; i++)
            {
                AnimatorState deathState = sm.AddState($"Death_{i}", new Vector3(240f + i * 220f, -110f, 0f));
                deathState.motion = deathClips[i];

                AnimatorStateTransition anyToDeath = sm.AddAnyStateTransition(deathState);
                anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");
                anyToDeath.AddCondition(AnimatorConditionMode.Equals, i, "DeathVariant");
                anyToDeath.canTransitionToSelf = false;
                anyToDeath.hasExitTime = false;
                anyToDeath.duration = 0.12f;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddCrawlEnter(AnimatorState from, AnimatorState crawlState)
        {
            AnimatorStateTransition tr = from.AddTransition(crawlState);
            tr.AddCondition(AnimatorConditionMode.If, 0f, "IsCrawling");
            tr.hasExitTime = false;
            tr.duration = 0.22f;
        }

        private static void NormalizeZombieHeight(GameObject visual, Animator anim, float targetHeight)
        {
            if (visual == null) return;

            float currentHeight = 0f;
            float minY = float.MaxValue;

            if (anim != null && anim.isHuman)
            {
                Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
                Transform lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                if (head != null && (lFoot != null || rFoot != null))
                {
                    float footY = lFoot != null && rFoot != null
                        ? Mathf.Min(lFoot.position.y, rFoot.position.y)
                        : (lFoot != null ? lFoot.position.y : rFoot.position.y);
                    // От стоп до макушки: голова + ~0.18 от роста на череп и подошву.
                    currentHeight = (head.position.y - footY) * 1.16f;
                    minY = footY - currentHeight * 0.05f;
                }
            }

            if (currentHeight < 0.001f)
            {
                Bounds b = new Bounds(visual.transform.position, Vector3.zero);
                bool hasBounds = false;
                foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
                {
                    if (!hasBounds) { b = r.bounds; hasBounds = true; }
                    else b.Encapsulate(r.bounds);
                }
                if (hasBounds)
                {
                    currentHeight = b.size.y;
                    minY = b.min.y;
                }
            }

            if (currentHeight > 0.0005f)
            {
                float factor = Mathf.Clamp(targetHeight / currentHeight, 0.2f, 200f);
                visual.transform.localScale *= factor;
                if (minY != float.MaxValue)
                {
                    visual.transform.localPosition = new Vector3(0f, -minY * factor, 0f);
                }
                Debug.Log($"[Ogni] Рост зомби нормализован: исходный={currentHeight:0.000}м -> масштаб x{factor:0.00} (итог ~{targetHeight:0.00}м).");
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
