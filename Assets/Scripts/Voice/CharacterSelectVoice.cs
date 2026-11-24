using System.Text;
using UnityEngine;

// Voz en la SELECCIÓN DE PILOTO. CharacterSelect lo añade solo.
//  - "nombre" (o "deletrear", "cambiar nombre") → modo DELETREO: se dicen las letras una por una
//    ("a", "be", "ce", "de"…) y se van escribiendo en la casilla con sonido de teclado.
//    "espacio" separa palabras, "borrar letra" quita la última, "borrar todo" vacía la casilla,
//    "finalizar" (o "listo", "terminar") deja de escuchar letras y "cancelar" recupera el nombre anterior.
//  - "borrar nombre", "hombre", "mujer", "fácil", "intermedio", "avanzado",
//    "destreza", "memoria", "misión final" (y "volver", que es un botón).
[RequireComponent(typeof(CharacterSelect))]
public class CharacterSelectVoice : MonoBehaviour
{
    // Letra → cómo se dice. B y V suenan igual en español: B = "be", V = "uve" o "ve chica".
    static readonly (char letter, string[] words)[] Letters =
    {
        ('A', new[] { "a" }),
        ('B', new[] { "be", "be grande", "be larga" }),
        ('C', new[] { "ce" }),
        ('D', new[] { "de" }),
        ('E', new[] { "e" }),
        ('F', new[] { "efe" }),
        ('G', new[] { "ge" }),
        ('H', new[] { "hache" }),
        ('I', new[] { "i", "i latina" }),
        ('J', new[] { "jota" }),
        ('K', new[] { "ka" }),
        ('L', new[] { "ele" }),
        ('M', new[] { "eme" }),
        ('N', new[] { "ene" }),
        ('Ñ', new[] { "eñe" }),
        ('O', new[] { "o" }),
        ('P', new[] { "pe" }),
        ('Q', new[] { "cu" }),
        ('R', new[] { "erre", "ere" }),
        ('S', new[] { "ese" }),
        ('T', new[] { "te" }),
        ('U', new[] { "u" }),
        ('V', new[] { "uve", "ve chica", "ve corta", "ve pequeña" }),
        ('W', new[] { "doble ve", "uve doble", "doble u" }),
        ('X', new[] { "equis" }),
        ('Y', new[] { "ye", "i griega" }),
        ('Z', new[] { "zeta", "seta" }),
    };

    // Lo que el navegador puede escribir en vez del nombre de la letra (no se usan en Windows).
    static readonly (char letter, string[] words)[] WebAliases =
    {
        ('A', new[] { "ha" }), ('B', new[] { "b" }), ('C', new[] { "c", "se" }), ('D', new[] { "d" }),
        ('E', new[] { "he" }), ('F', new[] { "f" }), ('G', new[] { "g", "je" }), ('H', new[] { "h" }),
        ('I', new[] { "y" }),   // la letra "i" suele llegar escrita como la conjunción "y"
        ('J', new[] { "j" }), ('K', new[] { "k", "ca" }), ('L', new[] { "l" }), ('M', new[] { "m" }),
        ('N', new[] { "n" }), ('Ñ', new[] { "ñ" }), ('O', new[] { "oh" }), ('P', new[] { "p" }),
        ('Q', new[] { "q" }), ('R', new[] { "r" }), ('S', new[] { "s" }), ('T', new[] { "t" }),
        ('V', new[] { "v" }), ('W', new[] { "w" }), ('X', new[] { "x" }), ('Z', new[] { "z" }),
    };

    CharacterSelect select;
    VoiceContext context;
    VoiceContext spellContext;
    readonly StringBuilder spelled = new StringBuilder();
    string previousName = "";
    bool spelling;

    void Awake()
    {
        select = GetComponent<CharacterSelect>();
        context = new VoiceContext(
            "VOZ:  NOMBRE (deletrea tu nombre)  ·  BORRAR NOMBRE  ·  HOMBRE  ·  MUJER  ·  FÁCIL  ·  INTERMEDIO  ·  " +
            "AVANZADO  ·  DESTREZA  ·  MEMORIA  ·  MISIÓN FINAL  ·  VOLVER")
            .On(StartSpelling, "nombre", "dictar nombre", "cambiar nombre", "deletrear", "mi nombre")
            .On(select.ClearName, "borrar nombre", "borrar")
            .On(select.SelectMale, "hombre", "masculino")
            .On(select.SelectFemale, "mujer", "femenino")
            .On(select.SelectFacil, "fácil")
            .On(select.SelectIntermedio, "intermedio")
            .On(select.SelectAvanzado, "avanzado")
            .On(select.PlayDestreza, "destreza", "prueba de destreza")
            .On(select.PlayMemoria, "memoria", "prueba de memoria")
            .On(select.PlayDefensa, "misión final", "defensa", "misión");

        spellContext = new VoiceContext(
            "DELETREA TU NOMBRE, letra por letra:\n" +
            "A · BE · CE · DE · E · EFE · GE · HACHE · I · JOTA · KA · ELE · EME · ENE · EÑE · O · PE · CU · " +
            "ERRE · ESE · TE · U · UVE · DOBLE VE · EQUIS · YE · ZETA\n" +
            "ESPACIO  ·  BORRAR LETRA  ·  BORRAR TODO  ·  FINALIZAR  ·  CANCELAR")
            .On(AddSpace, "espacio")
            .On(DeleteLetter, "borrar letra", "borrar", "atrás", "quitar")
            .On(DeleteAll, "borrar todo")
            .On(() => StopSpelling(true), "finalizar", "terminar", "listo", "fin")
            .On(() => StopSpelling(false), "cancelar");

        foreach (var (letter, words) in Letters)
        {
            char c = letter;
            spellContext.On(() => AddLetter(c), words);
        }
        foreach (var (letter, words) in WebAliases)
        {
            char c = letter;
            spellContext.Alias(() => AddLetter(c), words);
        }
    }

    void OnEnable() => VoiceCommands.Push(context);

    void OnDisable()
    {
        VoiceCommands.Pop(spellContext);
        VoiceCommands.Pop(context);
        spelling = false;
        if (select != null) select.SetDictating(false);
    }

    // ---------- Deletreo ----------

    void StartSpelling()
    {
        if (spelling) return;
        spelling = true;
        previousName = select.PilotName;
        spelled.Clear();
        select.SetPilotName("");
        VoiceCommands.Push(spellContext);   // mientras tanto sólo se escuchan letras y sus órdenes
        select.SetDictating(true);
    }

    void StopSpelling(bool keep)
    {
        if (!spelling) return;
        spelling = false;
        VoiceCommands.Pop(spellContext);
        if (!keep || spelled.ToString().Trim().Length == 0) select.SetPilotName(previousName);
        select.SetDictating(false);
    }

    void AddLetter(char letter)
    {
        if (!spelling || spelled.Length >= PilotData.MaxNameLength) return;
        bool wordStart = spelled.Length == 0 || spelled[spelled.Length - 1] == ' ';
        spelled.Append(wordStart ? char.ToUpperInvariant(letter) : char.ToLowerInvariant(letter));
        Show();
    }

    void AddSpace()
    {
        if (!spelling || spelled.Length == 0 || spelled[spelled.Length - 1] == ' ') return;
        if (spelled.Length >= PilotData.MaxNameLength - 1) return;   // no cabría otra letra
        spelled.Append(' ');
        KeySound.Play();
    }

    void DeleteLetter()
    {
        if (!spelling || spelled.Length == 0) return;
        spelled.Length--;
        if (spelled.Length > 0 && spelled[spelled.Length - 1] == ' ') spelled.Length--;
        Show();
    }

    void DeleteAll()
    {
        if (!spelling || spelled.Length == 0) return;
        spelled.Clear();
        Show();
    }

    void Show()
    {
        select.SetPilotName(spelled.ToString());
        KeySound.Play();
    }
}
