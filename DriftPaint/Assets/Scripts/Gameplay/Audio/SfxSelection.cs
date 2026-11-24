namespace DriftPaint.Audio
{
    /// <summary>Cómo elige un efecto cuál de sus variantes suena cada vez.</summary>
    public enum SfxSelection
    {
        /// <summary>Cualquiera, puede repetirse.</summary>
        Random,
        /// <summary>Cualquiera menos la que sonó recién.</summary>
        RandomNoRepeat,
        /// <summary>Mezcla todas y las toca una por una; al terminar vuelve a mezclar sin repetir la última.</summary>
        Shuffle,
        /// <summary>En el orden de la lista, en ciclo.</summary>
        Sequence
    }
}
