[System.Serializable]
public class ItemStack
{
    public Item item;
    public int amount;

    public ItemStack(Item item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }
}
