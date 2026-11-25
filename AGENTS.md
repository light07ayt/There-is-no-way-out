# CLAUDE.md — Проект «Огни в тумане»

Ты — технический руководитель, геймдизайнер и Unity-разработчик проекта **«Огни в тумане»** (кооперативный survival horror, Unity 6, C#, Netcode for GameObjects, 1–4 игрока, строго от первого лица).

## Правила работы
1. Пользователь — руководитель проекта. Не меняй утверждённые механики самостоятельно и не добавляй новые игровые системы без обсуждения.
2. Не начинай реализацию новой крупной системы без явного «СОГЛАСОВАНО» от пользователя (для исправления багов и мелких доработок можно сразу править код и проверять).
3. После любых правок C#-скриптов обязательно запускай `Check_Unity_Compile.bat` в корне проекта (Windows; Unity 6000.6.9f1) и проверяй `compile_check.log` на наличие `error CS`.
4. Не выдавай предположения за факты и не придумывай результаты тестов.

## Архитектура проекта (`Assets/_Project/Scripts`)
- `Core/`: `IInteractable`, `IDamageable`, `NoiseBus`, `WorldEvents`/`IWorldIndexed`, `CharacterAppearanceData` (`CharacterAppearance` для сети, `CharacterProfileStore` — пресеты и сохранение внешности в PlayerPrefs).
- `Player/`: `PlayerInputReader`, `FirstPersonMotor` (включая урон от падения), `FirstPersonCamera`, `PlayerInteractor`, `FlashlightController` (`F`), `PlayerHealth` (60 сек. критического состояния), `PlayerCraftMenu` (`Tab`/`I` — инвентарь, экипировка, крафт), `PlayerEquipment` (слоты 1–7: кулаки, топор, молоток, пистолет, дубина, дробовик, лук), `PistolController`, `ShotgunController`, `BowController` (натяжение ЛКМ, стрелы), `PlayerActionBlock` (блокировка оружия в стройке/меню), `PlayerAppearanceRig` (процедурная модель персонажа), `PlayerAppearanceSync` (сетевая синхронизация внешности), `NetworkNoiseRelay`.
- `Resources/`: `ResourceType` (включая `Ammo`, `Planks`, `Arrows`), `PlayerInventory`, `ResourcePickup`, `TreeResource`.
- `Building/`: `Barricade`, `Door`, `Campfire` (анимированные языки пламени), `Workbench`, `BuildPiece`/`BuildPieces` (таблица построек), `BuildCatalog` (каталог префабов), `PlayerBuilder` (`B` — режим строительства), `TrapPiece` (шипы/сигнализация).
- `Entities/`: `Listener/ListenerAI` (слепой, FSM `Patrol -> Investigate -> Chase`, `IDamageable`, анимация ходьбы, `Linecast`), `MonsterAI` (каркас ИИ Тьмы/Подражателя для этапа 7, использует `LightRegistry`).
- `World/`: `DayNightCycle` (`F2` +3 ч), `RuntimeWorldGenerator` (автогенерация по сетевому `Seed`), `FlickerLight`, `LightRegistry` (авто-реестр источников света), `WorldSpawnGuard` (подстраховка спавна сетевых объектов).
- `Vehicles/`: `DriveableCar` (управляемая машина, `IInteractable`, Netcode).
- `Net/`: `GameSession` (Соло / Хост / Клиент по IP), `NetworkPlayer`, `DebugConnectionHud` (`F1` — главное меню, лобби, редактор персонажа с 3D-превью, список игроков).
- `UI/`: `DebugPromptView` (подсказки взаимодействия и текущего слота).
- `Editor/`: `WorldSceneBuilder` (`Ogni -> Build World Scene`), `SandboxSceneBuilder` (в т.ч. сборка `Player.prefab`), `ApartmentBlockBuilder`, `LoftedCarBuilder` (4 типа машин), `CityPbr` (`Ogni -> Reimport City Textures`), `CityDetailBuilder`, `ListenerModelSetup`, `BuildCatalogSetup`, `ProceduralMaterialLibrary`, `ProceduralMeshBuilder`, `AbandonedMaterials`, `PropKit`, `MeshKit` (`Tube`, `Loft`, `Lathe`).

## Очередность этапов
- Этапы 1, 2а, 3 и 5 (мир 460×460 м, автогенерация, 7 высоток, машины, ресурсы, крафт по Tab, пистолет, дробовик, лук) — реализованы.
- Этап 2б: лобби-меню, редактор персонажа и сетевая синхронизация внешности — реализованы; Relay-подключение — впереди.
- Впереди: Этап 3б (проработка мира), Этап 4 (ИИ-напарник и соло-баланс), Этап 6 (вылазки и лут-контейнеры), Этап 7 (полный ИИ Тьмы, Подражателя и Кицунэ), Этапы 8–10 (Директор страха, сложности, сохранения, кампания, бесконечный режим).
