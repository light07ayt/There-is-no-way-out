namespace Ogni.Resources
{
    public enum ResourceType
    {
        Wood = 0,
        Stone = 1,
        Metal = 2,
        Cloth = 3,
        Fuel = 4,
        Medicine = 5,
        Battery = 6,
        Ammo = 7,
        Planks = 8,
        Arrows = 9
    }

    public static class ResourceNames
    {
        public static string GetRu(ResourceType type)
        {
            return type switch
            {
                ResourceType.Wood => "Дерево",
                ResourceType.Stone => "Камень",
                ResourceType.Metal => "Металл",
                ResourceType.Cloth => "Ткань",
                ResourceType.Fuel => "Топливо",
                ResourceType.Medicine => "Медикаменты",
                ResourceType.Battery => "Батарейки",
                ResourceType.Ammo => "Патроны",
                ResourceType.Planks => "Доски",
                ResourceType.Arrows => "Стрелы",
                _ => type.ToString()
            };
        }
    }
}
