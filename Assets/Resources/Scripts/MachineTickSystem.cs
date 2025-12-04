using UnityEngine;
using System.Collections.Generic;

public class MachineTickSystem : MonoBehaviour
{
    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;

        // Run once per second
        if (timer >= 1f)
        {
            timer = 0f;
            TickAllMachines();
        }
    }

    void TickAllMachines()
    {
        foreach (var po in FindObjectsOfType<PlaceableObject>())
        {
            if (po.blockDefinition == null) continue;

            po.blockDefinition.ProcessTick(po);
        }
    }
}