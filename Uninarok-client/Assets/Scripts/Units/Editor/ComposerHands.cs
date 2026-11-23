namespace Uninarok.Units.Editor
{
    /// <summary>
    /// How the composer passes a hand (PLAN-0009 4.11 "Composer", D35): the value as typed, the way the server sends it. A value
    /// of <see cref="WeaponHands.FirstItemId"/> or more is an item id and sets only the item field; a smaller one is a view id
    /// and sets only the view field.
    /// </summary>
    public static class ComposerHands
    {
        /// <summary>Splits a typed hand value into the look's view id and item id fields (one of them is 0).</summary>
        public static void Split(int value, out int viewId, out int itemId)
        {
            bool item = value > 0 && WeaponHands.IsItemId(value);
            itemId = item ? value : 0;
            viewId = item ? 0 : value;
        }

        /// <summary>Puts the right (weapon field) and left (shield field) hands, as typed, into a look.</summary>
        public static void Apply(ref UnitLook look, int right, int left)
        {
            Split(right, out look.Weapon, out look.WeaponItem);
            Split(left, out look.Shield, out look.ShieldItem);
        }
    }
}
