using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class MonsterAnimationShowcase : MonoBehaviour
{
    public const int PageSize = 20;
    private const int PageColumns = 5;
    private const float MotionSeconds = 2.4f;
    private static readonly int BaseColor =
        Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty =
        Shader.PropertyToID("_Color");

    private static readonly string[] MotionNames =
    {
        "Idle", "Run", "Attack", "Skill Attack",
        "Struggle", "Capture", "Death"
    };

    private readonly List<GameObject> actors = new List<GameObject>();
    private readonly List<TextMesh> labels = new List<TextMesh>();
    private List<MonsterDefinition> definitions;
    private Mesh tileMesh;
    private Material tileMaterial;
    private int page;
    private int motion;
    private float motionTimer;
    private bool automatic = true;

    public static int PageCount(int count)
    {
        return Mathf.Max(1, Mathf.CeilToInt(count / (float)PageSize));
    }

    private void Start()
    {
        MonsterDatabase database =
            Resources.Load<MonsterDatabase>("MonsterData/MonsterDatabase");
        definitions = database == null
            ? new List<MonsterDefinition>()
            : database.monsters
                .Where(definition =>
                    definition != null &&
                    int.TryParse(definition.serialNumber, out int serial) &&
                    serial >= 1 &&
                    serial <= 80 &&
                    !string.IsNullOrEmpty(definition.resourcePath))
                .OrderBy(definition => definition.serialNumber)
                .ToList();
        BuildArena();
        FrameCamera();
        ShowPage(0);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) ||
            Input.GetKeyDown(KeyCode.A))
            ShowPage(page - 1);
        if (Input.GetKeyDown(KeyCode.RightArrow) ||
            Input.GetKeyDown(KeyCode.D))
            ShowPage(page + 1);
        if (Input.GetKeyDown(KeyCode.Space))
            automatic = !automatic;
        for (int index = 0; index < MotionNames.Length; index++)
            if (Input.GetKeyDown(
                    (KeyCode)((int)KeyCode.Alpha1 + index)))
            {
                automatic = false;
                PlayMotion(index);
            }

        if (automatic)
        {
            motionTimer += Time.deltaTime;
            if (motionTimer >= MotionSeconds)
                PlayMotion((motion + 1) % MotionNames.Length);
        }

        Camera camera = Camera.main;
        if (camera != null)
            foreach (TextMesh label in labels)
                if (label != null)
                    label.transform.rotation = camera.transform.rotation;
    }

    private void ShowPage(int requestedPage)
    {
        int pageCount = PageCount(definitions == null ? 0 : definitions.Count);
        page = (requestedPage % pageCount + pageCount) % pageCount;
        foreach (GameObject actor in actors)
            if (actor != null)
                Destroy(actor);
        actors.Clear();
        labels.Clear();

        foreach (MonsterDefinition definition in definitions
                     .Skip(page * PageSize)
                     .Take(PageSize))
        {
            int index = actors.Count;
            GameObject prefab =
                Resources.Load<GameObject>(definition.resourcePath);
            if (prefab == null)
                continue;
            GameObject actor = Instantiate(prefab, transform);
            actor.name =
                definition.serialNumber + ". " + definition.displayName;
            Vector2Int coordinate = new Vector2Int(
                4 + index % PageColumns * 2,
                3 + index / PageColumns * 2);
            actor.transform.position =
                MonsterAutoBattlePrototype.CenteredGridWorld(coordinate) +
                Vector3.up *
                MonsterAutoBattlePrototype.PixelMonsterGroundOffset;
            actor.transform.rotation =
                Quaternion.LookRotation(Vector3.back, Vector3.up);
            actors.Add(actor);

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(actor.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text =
                definition.serialNumber + " " + definition.displayName;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 36;
            label.characterSize = 0.055f;
            label.color = new Color(0.86f, 0.94f, 1f);
            label.GetComponent<MeshRenderer>().sortingOrder = 20;
            labels.Add(label);
        }
        PlayMotion(motion);
    }

    private void PlayMotion(int index)
    {
        motion = Mathf.Clamp(index, 0, MotionNames.Length - 1);
        motionTimer = 0f;
        foreach (GameObject actor in actors)
        {
            SpriteMonsterAnimationDriver driver =
                actor.GetComponentInChildren<
                    SpriteMonsterAnimationDriver>(true);
            if (driver == null)
                continue;
            driver.ResetForReuse();
            switch (motion)
            {
                case 1:
                    driver.SetMoving(true);
                    break;
                case 2:
                    driver.PlayBasicAttack();
                    break;
                case 3:
                    driver.PlaySkillAttack();
                    break;
                case 4:
                    driver.SetStruggling(true);
                    break;
                case 5:
                    driver.PlayCapture();
                    break;
                case 6:
                    driver.PlayDeath();
                    break;
            }
        }
    }

    private void BuildArena()
    {
        tileMaterial =
            MonsterAutoBattlePrototype.CreateBattleTileMaterial();
        tileMesh = MonsterAutoBattlePrototype.CreateBattleTileMesh();

        Transform root = new GameObject("Square Tile Map").transform;
        root.SetParent(transform, false);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int row = 0;
             row < MonsterAutoBattlePrototype.Rows;
             row++)
        for (int column = 0;
             column < MonsterAutoBattlePrototype.Columns;
             column++)
        {
            Vector2Int coordinate = new Vector2Int(column, row);
            GameObject tile = new GameObject(
                "Square " + column + "," + row);
            tile.transform.SetParent(root, false);
            tile.transform.position =
                MonsterAutoBattlePrototype.CenteredGridWorld(coordinate);
            tile.AddComponent<MeshFilter>().sharedMesh = tileMesh;
            MeshRenderer renderer = tile.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = tileMaterial;
            Color color =
                MonsterAutoBattlePrototype.BattleTileColor(coordinate);
            block.Clear();
            block.SetColor(BaseColor, color);
            block.SetColor(ColorProperty, color);
            renderer.SetPropertyBlock(block);
        }
    }

    private static void FrameCamera()
    {
        Camera camera = Camera.main;
        MonsterAutoBattlePrototype.FrameCameraToBattleArena(
            camera,
            MonsterAutoBattlePrototype.CenteredArenaBounds());
    }

    private void OnGUI()
    {
        int pageCount = PageCount(definitions == null ? 0 : definitions.Count);
        GUI.Box(
            new Rect(18f, 18f, 520f, 70f),
            "MONSTER ANIMATION SHOWCASE  " +
            (page + 1) + "/" + pageCount + "\n" +
            "A/D or ←/→: page   Space: auto   1-7: motion   |   " +
            MotionNames[motion]);
    }

    private void OnDestroy()
    {
        if (tileMesh != null)
            Destroy(tileMesh);
        if (tileMaterial != null)
            Destroy(tileMaterial);
    }
}
