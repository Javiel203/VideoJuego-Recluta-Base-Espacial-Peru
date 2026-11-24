using System;
using System.Collections.Generic;

// Grupo de órdenes de voz de una pantalla o juego (por ejemplo, las del cohete).
// Se activa con VoiceCommands.Push(context) y se desactiva con VoiceCommands.Pop(context).
// Sólo escucha el último contexto activado; si una palabra no está en él, se prueba con
// los botones visibles de la pantalla (su texto es la orden de voz).
public sealed class VoiceContext
{
    readonly Dictionary<string, Action> actions = new Dictionary<string, Action>();
    readonly Dictionary<string, string> spoken = new Dictionary<string, string>();

    // Texto de ayuda que se muestra abajo a la izquierda mientras el contexto está activo.
    public string Help { get; set; }

    // true si el juego necesita el nivel de sonido del micrófono (propulsión del cohete).
    public bool UsesMicLevel { get; set; }

    public VoiceContext(string help) { Help = help; }

    // Asocia una o varias palabras (sinónimos) a una acción.
    public VoiceContext On(Action action, params string[] words)
    {
        foreach (string w in words)
        {
            string key = VoiceText.Normalize(w);
            if (key.Length == 0) continue;
            actions[key] = action;
            spoken[key] = w.ToLowerInvariant();
        }
        return this;
    }

    // Formas escritas que sólo llegan del navegador ("b", "y"…): se reconocen pero no se
    // añaden a la lista de palabras de Windows.
    public VoiceContext Alias(Action action, params string[] words)
    {
        foreach (string w in words)
        {
            string key = VoiceText.Normalize(w);
            if (key.Length > 0 && !actions.ContainsKey(key)) actions[key] = action;
        }
        return this;
    }

    internal bool TryGet(string phrase, out Action action) => actions.TryGetValue(phrase, out action);

    // normalizada → como se pronuncia (con tildes), para el reconocedor de Windows.
    internal IEnumerable<KeyValuePair<string, string>> Words => spoken;
}
