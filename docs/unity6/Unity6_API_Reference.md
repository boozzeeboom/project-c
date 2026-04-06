# Unity 6 Scripting API Reference

## Input System (Новая система ввода)

### Установка
Window → Package Manager → Input System → Install

### Базовое использование

```csharp
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    private PlayerInputActions inputActions;
    
    private void Awake()
    {
        inputActions = new PlayerInputActions();
        inputActions.Player.Enable();
    }
    
    private void Update()
    {
        // Чтение движения
        Vector2 moveInput = inputActions.Player.Move.ReadValue<Vector2>();
        
        // Чтение прыжка
        bool jumpPressed = inputActions.Player.Jump.WasPressedThisFrame();
        
        // Чтение спринта
        bool sprintHeld = inputActions.Player.Sprint.IsPressed();
    }
    
    private void OnEnable() => inputActions.Player.Enable();
    private void OnDisable() => inputActions.Player.Disable();
}
```

### Генерация класса Actions

1. Создать файл `.inputactions` (Assets → Create → Input Actions)
2. Настроить Actions:
   - Player → Move (Vector2, WASD/Joystick)
   - Player → Jump (Button, Space)
   - Player → Sprint (Button, LeftShift)
   - Player → Look (Vector2, Mouse)
   - Combat → Fire (Button, Mouse0)
   - Combat → Aim (Button, Mouse1)
3. Generate C# Class → использовать в коде

## Animator

### Базовое использование

```csharp
public class PlayerAnimator : MonoBehaviour
{
    private Animator animator;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
    }
    
    public void UpdateAnimations(float speed, bool isGrounded)
    {
        animator.SetFloat(SpeedHash, speed);
        animator.SetBool(IsGroundedHash, isGrounded);
    }
    
    public void TriggerJump()
    {
        animator.SetTrigger(JumpHash);
    }
}
```

### Анимационные параметры

| Параметр | Тип | Описание |
|----------|-----|----------|
| Speed | Float | Скорость движения (0-1) |
| IsGrounded | Bool | На земле ли персонаж |
| Jump | Trigger | Анимация прыжка |
| Attack | Trigger | Анимация атаки |
| IsSprinting | Bool | Режим спринта |
| Health | Float | Здоровье для анимации урона |

## Physics

### Raycast

```csharp
public bool GroundCheck()
{
    RaycastHit hit;
    float radius = 0.3f;
    float maxDistance = 1.1f;
    
    return Physics.SphereCast(
        transform.position + Vector3.up * 0.2f,
        radius,
        Vector3.down,
        out hit,
        maxDistance,
        groundLayer
    );
}
```

### LayerMask

```csharp
[SerializeField] private LayerMask groundLayer;
[SerializeField] private LayerMask enemyLayer;

// Проверка столкновения
void OnCollisionEnter(Collision collision)
{
    if (((1 << collision.gameObject.layer) & enemyLayer) != 0)
    {
        TakeDamage(10f);
    }
}
```

## Coroutines

```csharp
private IEnumerator FadeRoutine()
{
    float duration = 1f;
    float elapsed = 0f;
    
    while (elapsed < duration)
    {
        float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
        material.SetFloat("_Alpha", alpha);
        elapsed += Time.deltaTime;
        yield return null;
    }
}

// Запуск
StartCoroutine(FadeRoutine());

// Остановка
StopAllCoroutines();
```

## SceneManager

```csharp
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public async void LoadSceneAsync(string sceneName)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;
        
        while (!asyncLoad.isDone)
        {
            if (asyncLoad.progress >= 0.9f)
            {
                asyncLoad.allowSceneActivation = true;
            }
            yield return null;
        }
    }
    
    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
```

## PlayerPrefs (Сохранения)

```csharp
public class SaveSystem
{
    public static void SaveInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }
    
    public static int LoadInt(string key, int defaultValue = 0)
    {
        return PlayerPrefs.GetInt(key, defaultValue);
    }
}
```

## JSON Serialization

```csharp
using System.IO;
using UnityEngine;

[System.Serializable]
public class GameData
{
    public int playerLevel;
    public float playerHealth;
    public Vector3 playerPosition;
    public string[] inventory;
}

public class JsonSaveSystem
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
    
    public static void Save(GameData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
    }
    
    public static GameData Load()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            return JsonUtility.FromJson<GameData>(json);
        }
        return new GameData();
    }
}
```

## Object Pooling Pattern

```csharp
public class ProjectilePool : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private int poolSize = 20;
    
    private Queue<GameObject> pool = new();
    
    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            var obj = Instantiate(projectilePrefab, transform);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }
    
    public GameObject GetProjectile()
    {
        if (pool.Count > 0)
        {
            return pool.Dequeue();
        }
        
        var newProj = Instantiate(projectilePrefab, transform);
        return newProj;
    }
    
    public void ReturnProjectile(GameObject obj)
    {
        obj.SetActive(false);
        pool.Enqueue(obj);
    }
}
```

## Singleton Pattern

```csharp
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
```

## State Machine

```csharp
public interface IState
{
    void EnterState();
    void UpdateState();
    void ExitState();
}

public class StateMachine
{
    private IState currentState;
    
    public void SwitchState(IState newState)
    {
        currentState?.ExitState();
        currentState = newState;
        currentState?.EnterState();
    }
    
    public void Update()
    {
        currentState?.UpdateState();
    }
}

// Пример использования
public class IdleState : IState
{
    private PlayerController player;
    
    public IdleState(PlayerController player) => this.player = player;
    
    public void EnterState()
    {
        player.animator.SetFloat("Speed", 0);
    }
    
    public void UpdateState()
    {
        if (player.moveInput != Vector2.zero)
        {
            player.stateMachine.SwitchState(player.moveState);
        }
    }
    
    public void ExitState() { }
}
```

## ScriptableObject Database

```csharp
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Game/Data/ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemData> items = new();
    
    public ItemData GetItemById(int id)
    {
        return items.FirstOrDefault(i => i.id == id);
    }
    
    public ItemData GetItemByName(string name)
    {
        return items.FirstOrDefault(i => i.name == name);
    }
}

[System.Serializable]
public class ItemData
{
    public int id;
    public string itemName;
    public string description;
    public Sprite icon;
    public int value;
    public ItemType itemType;
}

public enum ItemType
{
    Weapon,
    Armor,
    Consumable,
    Quest
}
```

## UI Toolkit

```csharp
using UnityEngine.UIElements;

public class GameUI : MonoBehaviour
{
    private UIDocument document;
    private Label healthLabel;
    private ProgressBar healthBar;
    private Button pauseButton;
    
    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        var root = document.rootVisualElement;
        
        healthLabel = root.Q<Label>("HealthLabel");
        healthBar = root.Q<ProgressBar>("HealthBar");
        pauseButton = root.Q<Button>("PauseButton");
        
        pauseButton.clicked += OnPauseClicked;
    }
    
    private void OnDisable()
    {
        pauseButton.clicked -= OnPauseClicked;
    }
    
    private void OnPauseClicked()
    {
        Time.timeScale = Time.timeScale == 0 ? 1 : 0;
    }
    
    public void UpdateHealth(float current, float max)
    {
        healthLabel.text = $"{current}/{max}";
        healthBar.value = (current / max) * 100f;
    }
}
```

## Audio

```csharp
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    private AudioSource audioSource;
    
    [System.Serializable]
    public class Sound
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
    }
    
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }
    
    public void Play(string soundName)
    {
        var sound = sounds.FirstOrDefault(s => s.name == soundName);
        if (sound != null)
        {
            audioSource.PlayOneShot(sound.clip, sound.volume);
            audioSource.pitch = sound.pitch;
        }
    }
}
```

## Time Management

```csharp
public class TimeManager : MonoBehaviour
{
    public static void Pause() => Time.timeScale = 0;
    public static void Resume() => Time.timeScale = 1;
    public static bool IsPaused => Time.timeScale == 0;
    
    public static void SlowMotion(float factor = 0.5f)
    {
        Time.timeScale = factor;
        Time.fixedDeltaTime = 0.02f * factor;
    }
}
```

---

Это справочное руководство по основным Unity 6 API.
