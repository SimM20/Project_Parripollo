using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Grid
{
    private int width;
    private int height;
    private ItemType[,] items;

    public Grid(int Width = 10, int Height = 10)
    {
        width = Width;
        height = Height;
    }

    public abstract bool CanPlace(ItemType item, Vector2 size);

    public virtual void PlaceItem(ItemType item, Vector2 size)
    {
        
    }
}
