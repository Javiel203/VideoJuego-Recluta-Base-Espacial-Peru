using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class CollisionHandler : MonoBehaviour
{
    [SerializeField] float levelLoadDelay = 1f;
    [SerializeField] AudioClip crash;
    [SerializeField] AudioClip Success;
    [SerializeField] ParticleSystem crashParticles;
    [SerializeField] ParticleSystem successParticles;

    AudioSource audioSource;

    bool isControllable = true;
    bool isCollidable = true;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }
    void Update()
    {
        RespondToDebugKeys();
    }
    void OnCollisionEnter(Collision collision)
    {
        if (!isControllable || !isCollidable)
        {
            return;
        }
        switch (collision.gameObject.tag)
        {
            case "Friendly":
                // Plataforma de despegue: no pasa nada.
                break;
            case "Finish":
                StartSuccessSequence();
                break;
            default:
                StartCrashSequence();
                break;
        }
    }
    void RespondToDebugKeys()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            LoadNextLevel();
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            isCollidable = !isCollidable;
        }
    }
    void StartSuccessSequence()
    {
        isControllable = false;
        audioSource.Stop();
        audioSource.PlayOneShot(Success);
        successParticles.Play();
        GetComponent<Movment>().enabled = false;
        Invoke("LoadNextLevel", levelLoadDelay);
    }
    void StartCrashSequence()
    {
        isControllable = false;
        audioSource.Stop();
        audioSource.PlayOneShot(crash);
        crashParticles.Play();
        GetComponent<Movment>().enabled = false;

        // Prueba de destreza: 3 oportunidades para Under + Through + Over.
        int livesLeft = TrialProgress.LoseDestrezaLife();
        TrialNavigationHUD.NotifyCrash(livesLeft);
        if (livesLeft > 0)
            Invoke("ReloadLevel", levelLoadDelay);
        else
            Invoke("ShowDefeat", levelLoadDelay);
    }
    void ShowDefeat()
    {
        DefeatScreen.Show();
    }
    void ReloadLevel()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
    }
    void LoadNextLevel()
    {
        // Último nivel (Over): fin de la prueba de destreza, se muestra la pantalla de resultado.
        Scene current = SceneManager.GetActiveScene();
        int nextSceneIndex = current.buildIndex + 1;
        if (current.name == TrialProgress.FinalLevelScene || nextSceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            VictoryScreen.Show();
            return;
        }
        SceneManager.LoadScene(nextSceneIndex);
    }
}
