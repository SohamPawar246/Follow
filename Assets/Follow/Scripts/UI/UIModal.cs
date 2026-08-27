namespace Follow.UI
{
    /// <summary>
    /// How many interfaces are currently demanding the player's whole attention.
    ///
    /// The journal, the pause menu and the photograph review all take over the screen, and
    /// while any of them is up the things that live around the edges - the standing prompt,
    /// the compass markers - are noise on top of a card that is asking a question. Systems
    /// that can start something new check this before offering, so you can never end up
    /// fishing out of the back of the album.
    /// </summary>
    public static class UIModal
    {
        static int _open;

        public static bool Any => _open > 0;

        public static void Push() => _open++;

        public static void Pop() { if (_open > 0) _open--; }

        /// <summary>Called when a scene unloads, so a leaked push cannot wedge the game.</summary>
        public static void Clear() => _open = 0;
    }
}
