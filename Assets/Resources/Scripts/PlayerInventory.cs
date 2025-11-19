using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BlockStack
{
    [Tooltip("Which block type this slot holds. Null = empty slot.")]
    public BuildBlock block;

    [Tooltip("How many of this block are in this slot (0–maxStackSize).")]
    public int amount;
}

public class PlayerInventory : MonoBehaviour
{
    [Header("Capacity")]
    [Tooltip("Number of inventory/hotbar slots (e.g. 9 for keys 1–9).")]
    public int maxSlots = 9;

    [Tooltip("Maximum number of items per stack/slot.")]
    public int maxStackSize = 99;

    [Header("Slots (starting contents)")]
    [Tooltip("Each entry is a slot. In play mode these values will change.")]
    public List<BlockStack> slots = new List<BlockStack>();

    [Header("Selection")]
    [Tooltip("Currently selected hotbar slot index (0-based, so 0 = key 1, 8 = key 9).")]
    public int selectedSlotIndex = 0;

    public int SlotCount => slots != null ? slots.Count : 0;

    public BlockStack SelectedSlot
    {
        get
        {
            if (slots == null || slots.Count == 0) return null;
            selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, slots.Count - 1);
            return slots[selectedSlotIndex];
        }
    }

    public BuildBlock SelectedBlock => SelectedSlot != null ? SelectedSlot.block : null;

    public int SelectedAmount => SelectedSlot != null ? SelectedSlot.amount : 0;

    private void Awake()
    {
        EnsureSlotListSize();
        NormalizeAllSlots();
    }

    private void Update()
    {
        HandleHotbarInput();
    }

    private void HandleHotbarInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SelectSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) SelectSlot(3);
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) SelectSlot(4);
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) SelectSlot(5);
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) SelectSlot(6);
        if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) SelectSlot(7);
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) SelectSlot(8);
    }

    private void EnsureSlotListSize()
    {
        if (slots == null)
            slots = new List<BlockStack>();

        while (slots.Count < maxSlots)
        {
            slots.Add(new BlockStack { block = null, amount = 0 });
        }

        if (slots.Count > maxSlots)
        {
            slots.RemoveRange(maxSlots, slots.Count - maxSlots);
        }
    }

    private void NormalizeAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            NormalizeSlot(slots[i]);
        }
    }

    private void NormalizeSlot(BlockStack slot)
    {
        if (slot == null) return;

        if (slot.amount <= 0)
        {
            slot.amount = 0;
            slot.block = null;
            return;
        }

        if (slot.amount > maxStackSize)
            slot.amount = maxStackSize;
    }

    public void SelectSlot(int index)
    {
        EnsureSlotListSize();
        if (slots.Count == 0) return;

        index = Mathf.Clamp(index, 0, slots.Count - 1);
        var slot = slots[index];
        if (slot == null || slot.block == null || slot.amount <= 0)
        {
            return;
        }

        selectedSlotIndex = index;
    }

    public int GetAmount(BuildBlock block)
    {
        if (block == null || slots == null) return 0;

        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s.block == block)
                total += s.amount;
        }
        return total;
    }

    public bool HasBlock(BuildBlock block, int needed = 1)
    {
        return GetAmount(block) >= needed;
    }

    public bool TryConsume(BuildBlock block, int amount)
    {
        if (block == null || amount <= 0)
            return false;

        EnsureSlotListSize();

        if (!HasBlock(block, amount))
            return false;

        int remaining = amount;

        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            var s = slots[i];
            if (s.block != block || s.amount <= 0)
                continue;

            int take = Mathf.Min(s.amount, remaining);
            s.amount -= take;
            remaining -= take;

            NormalizeSlot(s);
        }

        return true;
    }

    public bool TryAddBlock(BuildBlock block, int amount)
    {
        if (block == null || amount <= 0)
            return false;

        EnsureSlotListSize();

        int remaining = amount;

        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            var s = slots[i];
            if (s.block != block) continue;

            int space = maxStackSize - s.amount;
            if (space <= 0) continue;

            int add = Mathf.Min(space, remaining);
            s.amount += add;
            remaining -= add;
            NormalizeSlot(s);
        }

        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            var s = slots[i];
            if (s.block != null && s.amount > 0) continue;

            s.block = block;
            s.amount = Mathf.Min(maxStackSize, remaining);
            remaining -= s.amount;
            NormalizeSlot(s);
        }

        return remaining == 0;
    }

    public bool AddBlock(BuildBlock block, int amount)
    {
        return TryAddBlock(block, amount);
    }
}
