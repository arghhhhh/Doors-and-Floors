using System.Collections.Generic;
using UnityEngine;

public class DoorPairGenerator : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Floors in order from bottom to top. Belt i hangs under floor i+1.")]
    public Transform[] floors; // Assign Floor_0 through Floor_5
    public float beltLeftBound = -7f;
    public float beltRightBound = 7f;
    public int minDoorsPerBelt = 3;
    public int maxDoorsPerBelt = 5;

    [Header("Door Prefab Settings")]
    [SerializeField] Shader spiralShader;
    public Vector3 doorSize = new Vector3(1.0f, 1.4f, 0.15f);
    public float doorHangDown = 0.9f; // How far doors hang below the ceiling

    [Header("Conveyor Settings")]
    public float conveyorSpeed = 1.5f;

    [Header("Colors")]
    [Tooltip("13 colors with evenly-spaced hues (27.7° apart) for maximum contrast")]
    public Color[] doorColors = new Color[]
    {
        new Color(1f, 0f, 0f),        // 0° Red
        new Color(1f, 0.46f, 0f),     // 27.7° Orange
        new Color(1f, 0.92f, 0f),     // 55.4° Gold
        new Color(0.62f, 1f, 0f),     // 83.1° Lime
        new Color(0.15f, 1f, 0f),     // 110.8° Green
        new Color(0f, 1f, 0.31f),     // 138.5° Spring green
        new Color(0f, 1f, 0.77f),     // 166.2° Turquoise
        new Color(0f, 0.77f, 1f),     // 193.8° Sky blue
        new Color(0f, 0.31f, 1f),     // 221.5° Blue
        new Color(0.15f, 0f, 1f),     // 249.2° Indigo
        new Color(0.62f, 0f, 1f),     // 276.9° Violet
        new Color(1f, 0f, 0.92f),     // 304.6° Magenta
        new Color(1f, 0f, 0.46f),     // 332.3° Rose
    };

    List<ConveyorBelt> belts = new List<ConveyorBelt>();
    List<List<PortalDoor>> beltDoors = new List<List<PortalDoor>>();

    public void Generate()
    {
        ClearExisting();
        AutoFindFloors();

        // Belt i hangs under floor i+1 (floors 1-5 have belts beneath them)
        int beltCount = floors.Length - 1;
        for (int i = 0; i < beltCount; i++)
        {
            Transform floorAbove = floors[i + 1];
            float floorY = floorAbove.position.y;
            float halfThickness = floorAbove.localScale.y * 0.5f;
            float ceilingBottomY = floorY - halfThickness;
            CreateBelt(i, ceilingBottomY);
        }

        PairDoors();
    }

    void AutoFindFloors()
    {
        if (floors != null && floors.Length > 0) return;

        // Find Floor_0 through Floor_N sorted by name
        List<Transform> found = new List<Transform>();
        for (int i = 0; i < 20; i++)
        {
            GameObject go = GameObject.Find($"Floor_{i}");
            if (go != null) found.Add(go.transform);
            else break;
        }
        floors = found.ToArray();
    }

    void ClearExisting()
    {
        foreach (var belt in belts)
        {
            if (belt != null) Destroy(belt.gameObject);
        }
        belts.Clear();
        beltDoors.Clear();
    }

    void CreateBelt(int floorIndex, float yPos)
    {
        GameObject beltObj = new GameObject($"ConveyorBelt_Floor{floorIndex}");
        beltObj.transform.parent = transform;
        beltObj.transform.position = new Vector3(0f, yPos - doorHangDown, 0f);

        ConveyorBelt belt = beltObj.AddComponent<ConveyorBelt>();
        belt.speed = conveyorSpeed;
        belt.leftBound = beltLeftBound;
        belt.rightBound = beltRightBound;
        belt.moveRight = (floorIndex % 2 == 0); // Alternate direction

        int doorCount = Random.Range(minDoorsPerBelt, maxDoorsPerBelt + 1);
        float spacing = (beltRightBound - beltLeftBound) / doorCount;

        List<PortalDoor> doors = new List<PortalDoor>();

        for (int d = 0; d < doorCount; d++)
        {
            float x = beltLeftBound + spacing * (d + 0.5f);
            GameObject doorObj = CreateDoorObject($"Door_F{floorIndex}_{d}", beltObj.transform, new Vector3(x, 0f, 0f));
            PortalDoor door = doorObj.GetComponent<PortalDoor>();
            doors.Add(door);
        }

        belts.Add(belt);
        beltDoors.Add(doors);
    }

    GameObject CreateDoorObject(string name, Transform parent, Vector3 localPos)
    {
        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = name;
        door.transform.parent = parent;
        door.transform.localPosition = localPos;
        door.transform.localScale = doorSize;

        // Make the visual collider a trigger
        BoxCollider col = door.GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.5f, 1f, 3f); // Trigger zone matches door height

        PortalDoor portal = door.AddComponent<PortalDoor>();
        portal.spiralShader = spiralShader;

        // Each door gets its own swivel animation
        portal.swivelCenter = Random.Range(75f, 105f);
        portal.swivelRange = Random.Range(15f, 30f);
        portal.swivelSpeed = Random.Range(0.8f, 2.0f);
        portal.swivelOffset = Random.Range(0f, Mathf.PI * 2f);

        // Random dark edge color (dark brown / dark grey / dark green range)
        portal.edgeColor = new Color(
            Random.Range(0.06f, 0.15f),
            Random.Range(0.06f, 0.14f),
            Random.Range(0.04f, 0.12f)
        );

        return door;
    }

    void PairDoors()
    {
        int colorIndex = 0;

        // Critical path: floor 0 -> floor 2 -> floor 4
        // Belt index maps: belt[i] is on ceiling of floor i, leads to floor i+1
        // A door on belt[0] (ceiling of floor 0) teleporting to belt[2] (ceiling of floor 2)
        // means: player on floor 0 jumps into belt[0] door, appears at belt[2] door (on floor 2's ceiling = floor 3 area)
        // Actually, let's think simpler: door on belt_i teleports player UP.
        // The paired door's teleportYOffset places them on the floor below that belt.

        // Critical path: belt_0 -> belt_2 -> belt_4 -> last belt
        // This guarantees a path from floor 0 all the way to the win floor
        int lastBelt = beltDoors.Count - 1;
        if (beltDoors.Count >= 5)
        {
            PairTwoDoors(beltDoors[0], beltDoors[2], ref colorIndex);
            PairTwoDoors(beltDoors[2], beltDoors[4], ref colorIndex);
            if (lastBelt > 4)
                PairTwoDoors(beltDoors[4], beltDoors[lastBelt], ref colorIndex);
        }

        // Gather all unpaired doors with their belt index
        int maxDistance = Mathf.Max(1, beltDoors.Count - 3); // total_floors - 3 (roof doesn't count)
        List<(PortalDoor door, int beltIndex)> unpaired = new List<(PortalDoor, int)>();
        for (int i = 0; i < beltDoors.Count; i++)
        {
            foreach (var door in beltDoors[i])
            {
                if (door.pairedDoor == null)
                    unpaired.Add((door, i));
            }
        }

        // Shuffle unpaired
        for (int i = unpaired.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (unpaired[i], unpaired[j]) = (unpaired[j], unpaired[i]);
        }

        // Pair with max floor distance constraint
        List<bool> paired = new List<bool>(new bool[unpaired.Count]);
        for (int i = 0; i < unpaired.Count; i++)
        {
            if (paired[i]) continue;

            // Find a valid partner within max distance
            for (int j = i + 1; j < unpaired.Count; j++)
            {
                if (paired[j]) continue;
                if (Mathf.Abs(unpaired[i].beltIndex - unpaired[j].beltIndex) > maxDistance) continue;

                unpaired[i].door.pairedDoor = unpaired[j].door;
                unpaired[j].door.pairedDoor = unpaired[i].door;
                ApplyPairVisuals(unpaired[i].door, unpaired[j].door, ref colorIndex);
                paired[i] = true;
                paired[j] = true;
                break;
            }
        }

        // Destroy leftover unpaired doors
        for (int i = 0; i < unpaired.Count; i++)
        {
            if (!paired[i])
            {
                beltDoors[unpaired[i].beltIndex].Remove(unpaired[i].door);
                Destroy(unpaired[i].door.gameObject);
            }
        }
    }

    void PairTwoDoors(List<PortalDoor> fromBelt, List<PortalDoor> toBelt, ref int colorIndex)
    {
        if (fromBelt.Count == 0 || toBelt.Count == 0) return;

        // Pick a random unpaired door from each belt
        PortalDoor doorA = GetRandomUnpaired(fromBelt);
        PortalDoor doorB = GetRandomUnpaired(toBelt);

        if (doorA == null || doorB == null) return;

        doorA.pairedDoor = doorB;
        doorB.pairedDoor = doorA;
        ApplyPairVisuals(doorA, doorB, ref colorIndex);
    }

    void ApplyPairVisuals(PortalDoor doorA, PortalDoor doorB, ref int colorIndex)
    {
        Color c = doorColors[colorIndex % doorColors.Length];
        colorIndex++;

        // Randomize spiral parameters — same for both doors in the pair
        float speed       = Random.Range(0.5f, 2.0f);
        float arms        = Mathf.Round(Random.Range(3f, 10f));
        float rings       = Mathf.Round(Random.Range(3f, 8f));
        float bands       = Mathf.Round(Random.Range(5f, 15f));
        float angle       = Random.Range(0.5f, 2.6f); // ~30° to ~150°
        float brightness  = Random.Range(1.1f, 1.5f);

        // Randomly flip spin direction per pair
        if (Random.value > 0.5f)
            speed = -speed;

        SetDoorSpiralParams(doorA, c, speed, arms, rings, bands, angle, brightness);
        SetDoorSpiralParams(doorB, c, speed, arms, rings, bands, angle, brightness);
    }

    void SetDoorSpiralParams(PortalDoor door, Color color, float speed, float arms,
        float rings, float bands, float angle, float brightness)
    {
        door.doorColor = color;
        door.spiralSpeed = speed;
        door.spiralArms = arms;
        door.spiralRings = rings;
        door.spiralBands = bands;
        door.spiralAngle = angle;
        door.spiralBrightness = brightness;
    }

    PortalDoor GetRandomUnpaired(List<PortalDoor> doors)
    {
        List<PortalDoor> unpaired = new List<PortalDoor>();
        foreach (var d in doors)
        {
            if (d.pairedDoor == null) unpaired.Add(d);
        }
        if (unpaired.Count == 0) return null;
        return unpaired[Random.Range(0, unpaired.Count)];
    }
}
 
