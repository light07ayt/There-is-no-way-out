using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Меню Ogni -> Setup Character Models.
    /// Настраивает все .fbx модели персонажей из Assets/_Project/Characters/Models/
    /// и отдельные анимации из Assets/_Project/Characters/Animations/:
    /// - Ставит Humanoid-риг, настраивает зацикливание и Bake Into Pose, извлекает текстуры/материалы (URP).
    /// - Собирает общий AnimatorController (Idle / Walk / Run / Jump / Attack / Death).
    /// - Создаёт готовые префабы в Assets/_Project/Resources/Characters/ для меню и мультиплеера.
    /// </summary>
    public static class CharacterModelsSetup
    {
        private const string ModelsFolder = "Assets/_Project/Characters/Models";
        private const string AnimFolder = "Assets/_Project/Characters/Animations";
        private const string TexturesFolder = "Assets/_Project/Characters/Textures";
        private const string MaterialsFolder = "Assets/_Project/Characters/Materials";
        private const string ResourcesCharsFolder = "Assets/_Project/Resources/Characters";
        private const string ControllerPath = "Assets/_Project/Characters/CharacterAnimator.controller";

        private enum ClipKind
        {
            Unknown,
            Idle,
            Walk,
            Run,
            Jump,
            Attack,
            AttackKick,
            Death
        }

        // Фильтр пака капоэйры: отбрасываем акробатику, сальто, колёса и танцы,
        // оставляя только модель (Ch02_nonPBR) и чистые стоячие удары ногой (bencao / pontera).
        private static readonly string[] FilteredCapoeiraTokens =
        {
            "capoeira", "ginga", "esquiva", "macaco", "rasteira", "armada",
            "queshada", "troca", "giratoria", "compasso", "chapaeu",
            "martelo", "meia lua", "chapa"
        };

        private static bool IsFilteredOutCapoeiraFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            string lower = fileName.ToLowerInvariant().Trim();
            if (lower == "au" || lower.StartsWith("au ")) return true;
            foreach (string token in FilteredCapoeiraTokens)
            {
                if (lower.Contains(token)) return true;
            }
            return false;
        }

        private static bool IsStandaloneAnimationFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            string lower = fileName.ToLowerInvariant();
            if (IsFilteredOutCapoeiraFile(lower)) return true;
            if (lower.Contains("bencao") || lower.Contains("pontera")) return true;
            return false;
        }

        [MenuItem("Ogni/Setup Character Models")]
        public static void Build()
        {
            EnsureFolder("Assets/_Project/Characters");
            EnsureFolder(ModelsFolder);
            EnsureFolder(AnimFolder);
            EnsureFolder(TexturesFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder("Assets/_Project/Resources");
            EnsureFolder(ResourcesCharsFolder);

            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder });
            string[] animGuids = AssetDatabase.FindAssets("t:Model", new[] { AnimFolder });

            if (modelGuids.Length == 0 && animGuids.Length == 0)
            {
                Debug.LogWarning("[Ogni] В папках " + ModelsFolder + " и " + AnimFolder + " не найдено ни одной FBX-модели.");
                return;
            }

            AnimationClip idleClip = null;
            AnimationClip walkClip = null;
            AnimationClip runClip = null;
            AnimationClip jumpClip = null;
            AnimationClip attackClip = null;
            AnimationClip attackKickClip = null;
            AnimationClip deathClip = null;
            int skippedCapoeira = 0;

            // 1. Настраиваем импорт всех моделей: Humanoid-риг, текстуры и материалы.
            foreach (string guid in modelGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string baseName = Path.GetFileNameWithoutExtension(path);
                if (IsFilteredOutCapoeiraFile(baseName))
                {
                    skippedCapoeira++;
                    continue;
                }
                ClipKind fileKind = ClassifyByName(baseName);
                bool shouldLoop = ShouldLoopClip(fileKind == ClipKind.Unknown ? ClipKind.Walk : fileKind);
                ConfigureModelImporter(path, extractMaterials: !IsStandaloneAnimationFile(baseName), loopTime: shouldLoop);

                CollectClipsFromAsset(path, ref idleClip, ref walkClip, ref runClip, ref jumpClip, ref attackClip, ref attackKickClip, ref deathClip, fallbackToWalk: !IsStandaloneAnimationFile(baseName));
            }

            // 2. Настраиваем отдельные FBX-анимации в Assets/_Project/Characters/Animations/.
            foreach (string guid in animGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string baseName = Path.GetFileNameWithoutExtension(path);
                if (IsFilteredOutCapoeiraFile(baseName))
                {
                    skippedCapoeira++;
                    continue;
                }
                ClipKind fileKind = ClassifyByName(baseName);
                bool shouldLoop = ShouldLoopClip(fileKind);
                ConfigureModelImporter(path, extractMaterials: false, loopTime: shouldLoop);

                CollectClipsFromAsset(path, ref idleClip, ref walkClip, ref runClip, ref jumpClip, ref attackClip, ref attackKickClip, ref deathClip, fallbackToWalk: false);
            }

            FixExtractedMaterials();

            // 3. Создаём/обновляем AnimatorController со всеми состояниями персонажа.
            AnimatorController controller = BuildAnimatorController(idleClip, walkClip, runClip, jumpClip, attackClip, attackKickClip, deathClip);

            // 4. Генерируем префабы в Resources/Characters/.
            int count = 0;
            foreach (string guid in modelGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string baseName = Path.GetFileNameWithoutExtension(path);
                if (IsStandaloneAnimationFile(baseName)) continue;
                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (modelAsset == null) continue;

                string cleanName = Path.GetFileNameWithoutExtension(path).Replace(" ", "_");
                string prefabPath = $"{ResourcesCharsFolder}/Char_{cleanName}.prefab";

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                Animator anim = instance.GetComponent<Animator>();
                if (anim == null) anim = instance.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Object.DestroyImmediate(instance);
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[Ogni] Настроено моделей персонажей: {count}. " +
                $"Клипы контроллера: Idle={(idleClip != null ? idleClip.name : "fallback")}, " +
                $"Walk={(walkClip != null ? walkClip.name : "-")}, " +
                $"Run={(runClip != null ? runClip.name : "-")}, " +
                $"Jump={(jumpClip != null ? jumpClip.name : "-")}, " +
                $"Attack={(attackClip != null ? attackClip.name : "-")}, " +
                $"AttackKick={(attackKickClip != null ? attackKickClip.name : "-")}, " +
                $"Death={(deathClip != null ? deathClip.name : "-")}, " +
                $"отфильтровано лишних клипов капоэйры: {skippedCapoeira}.");
        }

        private static ClipKind ClassifyByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return ClipKind.Unknown;
            string lower = name.ToLowerInvariant();

            if (lower.Contains("death") || lower.Contains("die") || lower.Contains("downed")) return ClipKind.Death;
            if (lower.Contains("jump")) return ClipKind.Jump;
            if (lower.Contains("bencao") || lower.Contains("pontera")) return ClipKind.AttackKick;
            if (lower.Contains("punch") || lower.Contains("attack") || lower.Contains("melee") || lower.Contains("swing") || lower.Contains("hit")) return ClipKind.Attack;
            if (lower.Contains("run") || lower.Contains("sprint")) return ClipKind.Run;
            if (lower.Contains("idle")) return ClipKind.Idle;
            if (lower.Contains("walk")) return ClipKind.Walk;

            return ClipKind.Unknown;
        }

        private static bool ShouldLoopClip(ClipKind kind)
        {
            return kind == ClipKind.Idle || kind == ClipKind.Walk || kind == ClipKind.Run || kind == ClipKind.Unknown;
        }

        private static void CollectClipsFromAsset(
            string assetPath,
            ref AnimationClip idleClip,
            ref AnimationClip walkClip,
            ref AnimationClip runClip,
            ref AnimationClip jumpClip,
            ref AnimationClip attackClip,
            ref AnimationClip attackKickClip,
            ref AnimationClip deathClip,
            bool fallbackToWalk)
        {
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (!(obj is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;

                ClipKind kind = ClassifyByName(fileName + " " + clip.name);
                switch (kind)
                {
                    case ClipKind.Idle:
                        idleClip = clip;
                        break;
                    case ClipKind.Walk:
                        walkClip = clip;
                        break;
                    case ClipKind.Run:
                        runClip = clip;
                        break;
                    case ClipKind.Jump:
                        jumpClip = clip;
                        break;
                    case ClipKind.Attack:
                        attackClip = clip;
                        break;
                    case ClipKind.AttackKick:
                        if (attackKickClip == null || fileName.ToLowerInvariant().Contains("bencao"))
                            attackKickClip = clip;
                        break;
                    case ClipKind.Death:
                        deathClip = clip;
                        break;
                    default:
                        if (fallbackToWalk && walkClip == null) walkClip = clip;
                        break;
                }
            }
        }

        private static void ConfigureModelImporter(string assetPath, bool extractMaterials = true, bool loopTime = true)
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

            // Настраиваем клипы: зацикливание и Bake Into Pose (чтобы персонаж не уплывал от CharacterController).
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
                    if (clips[i].loopTime != loopTime)
                    {
                        clips[i].loopTime = loopTime;
                        clipsDirty = true;
                    }
                    if (!clips[i].lockRootRotation)
                    {
                        clips[i].lockRootRotation = true;
                        clipsDirty = true;
                    }
                    if (!clips[i].keepOriginalOrientation)
                    {
                        clips[i].keepOriginalOrientation = true;
                        clipsDirty = true;
                    }
                    if (!clips[i].lockRootHeightY)
                    {
                        clips[i].lockRootHeightY = true;
                        clipsDirty = true;
                    }
                    if (!clips[i].keepOriginalPositionY)
                    {
                        clips[i].keepOriginalPositionY = true;
                        clipsDirty = true;
                    }
                    if (clips[i].heightFromFeet)
                    {
                        clips[i].heightFromFeet = false;
                        clipsDirty = true;
                    }
                    if (clips[i].lockRootPositionXZ)
                    {
                        clips[i].lockRootPositionXZ = false;
                        clipsDirty = true;
                    }
                    if (!clips[i].keepOriginalPositionXZ)
                    {
                        clips[i].keepOriginalPositionXZ = true;
                        clipsDirty = true;
                    }
                }

                if (clipsDirty)
                {
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
            }
        }

        private static void FixExtractedMaterials()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Characters" }))
            {
                string matPath = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) continue;

                string lower = mat.name.ToLowerInvariant();
                bool isHair = lower.Contains("hair") || lower.Contains("beard") || lower.Contains("brow") || lower.Contains("lash");

                if (mat.HasProperty("_Smoothness"))
                {
                    mat.SetFloat("_Smoothness", isHair ? 0.05f : 0.15f);
                }
                if (mat.HasProperty("_Glossiness"))
                {
                    mat.SetFloat("_Glossiness", isHair ? 0.05f : 0.15f);
                }
                if (isHair && mat.HasProperty("_AlphaClip"))
                {
                    mat.SetFloat("_AlphaClip", 1f);
                    if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.38f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                }

                EditorUtility.SetDirty(mat);
            }
        }

        private static AnimatorController BuildAnimatorController(
            AnimationClip idleClip,
            AnimationClip walkClip,
            AnimationClip runClip,
            AnimationClip jumpClip,
            AnimationClip attackClip,
            AnimationClip attackKickClip,
            AnimationClip deathClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "IsGrounded",
                type = AnimatorControllerParameterType.Bool,
                defaultBool = true
            });
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "IsDead",
                type = AnimatorControllerParameterType.Bool,
                defaultBool = false
            });
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackVariant", AnimatorControllerParameterType.Int);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition anyTr in sm.anyStateTransitions)
            {
                sm.RemoveAnyStateTransition(anyTr);
            }
            foreach (ChildAnimatorState s in sm.states)
            {
                sm.RemoveState(s.state);
            }

            // Пороги скорости совпадают с FirstPersonMotor (walk=3.5, sprint=5.5).
            const float WalkThreshold = 0.15f;
            const float RunEnterThreshold = 4.3f;
            const float RunExitThreshold = 4.0f;

            // --- Idle ---
            AnimatorState idleState = sm.AddState("Idle", new Vector3(260f, 120f, 0f));
            if (idleClip != null)
            {
                idleState.motion = idleClip;
            }
            else if (walkClip != null)
            {
                // Если отдельного клипа Idle нет, держим первый кадр ходьбы, чтобы риг не уходил в T-pose.
                idleState.motion = walkClip;
                idleState.speed = 0f;
            }
            else if (runClip != null)
            {
                idleState.motion = runClip;
                idleState.speed = 0f;
            }
            sm.defaultState = idleState;

            // --- Walk ---
            AnimatorState walkState = null;
            AnimationClip effectiveWalk = walkClip != null ? walkClip : runClip;
            if (effectiveWalk != null)
            {
                walkState = sm.AddState("Walk", new Vector3(520f, 120f, 0f));
                walkState.motion = effectiveWalk;
                if (walkClip == null) walkState.speed = 0.65f;

                AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
                idleToWalk.AddCondition(AnimatorConditionMode.Greater, WalkThreshold, "Speed");
                idleToWalk.hasExitTime = false;
                idleToWalk.duration = 0.15f;

                AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
                walkToIdle.AddCondition(AnimatorConditionMode.Less, WalkThreshold, "Speed");
                walkToIdle.hasExitTime = false;
                walkToIdle.duration = 0.15f;
            }

            // --- Run ---
            AnimatorState runState = null;
            if (runClip != null)
            {
                runState = sm.AddState("Run", new Vector3(780f, 120f, 0f));
                runState.motion = runClip;

                if (walkState != null)
                {
                    AnimatorStateTransition walkToRun = walkState.AddTransition(runState);
                    walkToRun.AddCondition(AnimatorConditionMode.Greater, RunEnterThreshold, "Speed");
                    walkToRun.hasExitTime = false;
                    walkToRun.duration = 0.15f;

                    AnimatorStateTransition runToWalk = runState.AddTransition(walkState);
                    runToWalk.AddCondition(AnimatorConditionMode.Less, RunExitThreshold, "Speed");
                    runToWalk.hasExitTime = false;
                    runToWalk.duration = 0.15f;
                }

                AnimatorStateTransition idleToRun = idleState.AddTransition(runState);
                idleToRun.AddCondition(AnimatorConditionMode.Greater, RunEnterThreshold, "Speed");
                idleToRun.hasExitTime = false;
                idleToRun.duration = 0.15f;

                AnimatorStateTransition runToIdle = runState.AddTransition(idleState);
                runToIdle.AddCondition(AnimatorConditionMode.Less, WalkThreshold, "Speed");
                runToIdle.hasExitTime = false;
                runToIdle.duration = 0.15f;
            }

            // --- Jump ---
            if (jumpClip != null)
            {
                AnimatorState jumpState = sm.AddState("Jump", new Vector3(520f, -40f, 0f));
                jumpState.motion = jumpClip;

                AnimatorStateTransition anyToJump = sm.AddAnyStateTransition(jumpState);
                anyToJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
                anyToJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
                anyToJump.canTransitionToSelf = false;
                anyToJump.hasExitTime = false;
                anyToJump.duration = 0.08f;

                if (runState != null)
                {
                    AnimatorStateTransition jumpToRun = jumpState.AddTransition(runState);
                    jumpToRun.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
                    jumpToRun.AddCondition(AnimatorConditionMode.Greater, RunEnterThreshold, "Speed");
                    jumpToRun.hasExitTime = false;
                    jumpToRun.duration = 0.12f;
                }

                if (walkState != null)
                {
                    AnimatorStateTransition jumpToWalk = jumpState.AddTransition(walkState);
                    jumpToWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
                    jumpToWalk.AddCondition(AnimatorConditionMode.Greater, WalkThreshold, "Speed");
                    if (runState != null)
                    {
                        jumpToWalk.AddCondition(AnimatorConditionMode.Less, RunEnterThreshold, "Speed");
                    }
                    jumpToWalk.hasExitTime = false;
                    jumpToWalk.duration = 0.12f;
                }

                AnimatorStateTransition jumpToIdle = jumpState.AddTransition(idleState);
                jumpToIdle.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
                jumpToIdle.AddCondition(AnimatorConditionMode.Less, WalkThreshold, "Speed");
                jumpToIdle.hasExitTime = false;
                jumpToIdle.duration = 0.12f;

                // Страховочный выход по окончании клипа, если персонаж уже на земле.
                AnimatorStateTransition jumpExitFallback = jumpState.AddTransition(idleState);
                jumpExitFallback.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
                jumpExitFallback.hasExitTime = true;
                jumpExitFallback.exitTime = 0.95f;
                jumpExitFallback.duration = 0.12f;
            }

            // --- Attack (удар кулаком / ближний бой) ---
            if (attackClip != null)
            {
                AnimatorState attackState = sm.AddState("Attack", new Vector3(520f, 270f, 0f));
                attackState.motion = attackClip;

                AnimatorStateTransition anyToAttack = sm.AddAnyStateTransition(attackState);
                anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                anyToAttack.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
                if (attackKickClip != null)
                {
                    anyToAttack.AddCondition(AnimatorConditionMode.Equals, 0, "AttackVariant");
                }
                anyToAttack.canTransitionToSelf = true;
                anyToAttack.hasExitTime = false;
                anyToAttack.duration = 0.08f;

                if (runState != null)
                {
                    AnimatorStateTransition attackToRun = attackState.AddTransition(runState);
                    attackToRun.AddCondition(AnimatorConditionMode.Greater, RunEnterThreshold, "Speed");
                    attackToRun.hasExitTime = true;
                    attackToRun.exitTime = 0.85f;
                    attackToRun.duration = 0.15f;
                }

                if (walkState != null)
                {
                    AnimatorStateTransition attackToWalk = attackState.AddTransition(walkState);
                    attackToWalk.AddCondition(AnimatorConditionMode.Greater, WalkThreshold, "Speed");
                    if (runState != null)
                    {
                        attackToWalk.AddCondition(AnimatorConditionMode.Less, RunEnterThreshold, "Speed");
                    }
                    attackToWalk.hasExitTime = true;
                    attackToWalk.exitTime = 0.85f;
                    attackToWalk.duration = 0.15f;
                }

                AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
                attackToIdle.hasExitTime = true;
                attackToIdle.exitTime = 0.85f;
                attackToIdle.duration = 0.15f;
            }

            // --- AttackKick (отфильтрованный стоячий удар ногой для девушки: bencao / pontera) ---
            if (attackKickClip != null)
            {
                AnimatorState kickState = sm.AddState("AttackKick", new Vector3(780f, 270f, 0f));
                kickState.motion = attackKickClip;

                AnimatorStateTransition anyToKick = sm.AddAnyStateTransition(kickState);
                anyToKick.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                anyToKick.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
                anyToKick.AddCondition(AnimatorConditionMode.Equals, 1, "AttackVariant");
                anyToKick.canTransitionToSelf = true;
                anyToKick.hasExitTime = false;
                anyToKick.duration = 0.08f;

                if (runState != null)
                {
                    AnimatorStateTransition kickToRun = kickState.AddTransition(runState);
                    kickToRun.AddCondition(AnimatorConditionMode.Greater, RunEnterThreshold, "Speed");
                    kickToRun.hasExitTime = true;
                    kickToRun.exitTime = 0.85f;
                    kickToRun.duration = 0.15f;
                }

                if (walkState != null)
                {
                    AnimatorStateTransition kickToWalk = kickState.AddTransition(walkState);
                    kickToWalk.AddCondition(AnimatorConditionMode.Greater, WalkThreshold, "Speed");
                    if (runState != null)
                    {
                        kickToWalk.AddCondition(AnimatorConditionMode.Less, RunEnterThreshold, "Speed");
                    }
                    kickToWalk.hasExitTime = true;
                    kickToWalk.exitTime = 0.85f;
                    kickToWalk.duration = 0.15f;
                }

                AnimatorStateTransition kickToIdle = kickState.AddTransition(idleState);
                kickToIdle.hasExitTime = true;
                kickToIdle.exitTime = 0.85f;
                kickToIdle.duration = 0.15f;
            }

            // --- Death / Downed ---
            if (deathClip != null)
            {
                AnimatorState deathState = sm.AddState("Death", new Vector3(260f, -40f, 0f));
                deathState.motion = deathClip;

                AnimatorStateTransition anyToDeath = sm.AddAnyStateTransition(deathState);
                anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
                anyToDeath.canTransitionToSelf = false;
                anyToDeath.hasExitTime = false;
                anyToDeath.duration = 0.15f;

                AnimatorStateTransition deathToIdle = deathState.AddTransition(idleState);
                deathToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
                deathToIdle.hasExitTime = false;
                deathToIdle.duration = 0.25f;
            }

            EditorUtility.SetDirty(controller);
            return controller;
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
