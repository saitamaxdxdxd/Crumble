namespace Crumble.Maze
{
    /// <summary>
    /// Tipos de celda que puede contener una casilla del maze.
    /// </summary>
    public enum CellType
    {
        WALL,
        PATH,
        ROOM,
        CORRIDOR,
        DOOR,
        START,
        EXIT,
        NARROW_06,
        NARROW_04,
        /// <summary>Trampa de un solo uso — se convierte en WALL al pisarla.</summary>
        TRAP_ONESHOT,
        /// <summary>Trampa de drenaje — consume tamaño cada vez que se pisa.</summary>
        TRAP_DRAIN,
        /// <summary>Pico — muerte instantánea al pisarlo, independientemente del tamaño.</summary>
        SPIKE
    }
}
