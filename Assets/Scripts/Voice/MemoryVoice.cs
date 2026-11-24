using UnityEngine;

// Voz en la PRUEBA DE MEMORIA. MemoryGame lo añade solo.
// Cada cristal se elige por su color o por su número (de izquierda a derecha):
//   AZUL = 1 ("uno"),  VERDE = 2 ("dos"),  NARANJA = 3 ("tres").
// Se pueden decir varios seguidos en una frase: "azul, verde, azul".
// JUGAR / REINTENTAR / ELEGIR PRUEBA funcionan solos porque son botones de la pantalla.
[RequireComponent(typeof(MemoryGame))]
public class MemoryVoice : MonoBehaviour
{
    MemoryGame game;
    VoiceContext context;

    void Awake()
    {
        game = GetComponent<MemoryGame>();
        context = new VoiceContext(
            "VOZ:  AZUL o UNO   ·   VERDE o DOS   ·   NARANJA o TRES   ·   JUGAR   ·   REINTENTAR")
            .On(() => game.SelectCrystal(0), "azul", "uno", "una")
            .On(() => game.SelectCrystal(1), "verde", "dos")
            .On(() => game.SelectCrystal(2), "naranja", "tres");
    }

    void OnEnable() => VoiceCommands.Push(context);
    void OnDisable() => VoiceCommands.Pop(context);
}
