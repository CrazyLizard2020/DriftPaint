namespace DriftPaint.Audio
{
    /// <summary>
    /// Contextos de música que no son mapas. Los mapas usan su propio identificador como contexto,
    /// así un mapa nuevo solo necesita una fila nueva en la biblioteca de música.
    /// </summary>
    public static class MusicContexts
    {
        /// <summary>Menú principal, buscador de salas y lobby: comparten música para que no se corte al navegar.</summary>
        public const string Menu = "menu";
    }
}
