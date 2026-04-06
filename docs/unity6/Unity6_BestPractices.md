# Unity 6 C# Scripting Best Practices & Guide

## 1. MonoBehaviour Lifecycle

### Правильный порядок вызова методов:
```
Awake() → OnEnable() → Start() → FixedUpdate() → Update() → LateUpdate() → OnDisable() → OnDestroy()
```

### Рекомендации:
- **Awake()**: Инициализация переменных, получение компонентов
- **OnEnable()**: Подписка на события, регистрация в менеджерах
- **Start()**: Инициализация, зависящая от других объектов
- **FixedUpdate()**: Физика (Rigidbody forces)
- **Update()**: Ввод, таймеры, анимации
- **LateUpdate()**: Камеры, финальные корректировки позиции
- **OnDisable()**: Отписка от событий, очистка

## 2. Современный C# в Unity 6

### Используем C# 9.0+ фичи:
```csharp
// Records для immutable данных
public readonly record struct Vector3Int(int x, int y, int z);

// Pattern matching
public string GetState() => health switch
{
    <= 0 => "Dead",
    < 30 => "Critical",
    < 70 => "Warning",
    _ => "Healthy"
};

// Init-only свойства
public class PlayerData
{
    public string Name { get; init; }
    public int MaxHealth { get; init; }
}
```

### Nullable reference types:
```csharp
#nullable enable
public class Player : MonoBehaviour
{
    [SerializeField] private Weapon? currentWeapon;
    
    public void EquipWeapon(Weapon weapon)
    {
        currentWeapon = weapon;
        currentWeapon?.Activate();
    }
}
```

## 3. Компонентная архитектура

### Правильная структура MonoBehaviour:
```csharp
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Основной контроллер игрока
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        
        [Header("Jump")]
        [SerializeField] private float jumpForce = 7f;
        [SerializeField] private LayerMask groundLayer;
        
        [Header("Components")]
        [SerializeField] private CharacterController controller;
        [SerializeField] private Animator animator;
        
        #endregion
        
        #region Private Fields
        
        private Vector3 velocity;
        private bool isGrounded;
        private bool isSprinting;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            ValidateComponents();
        }
        
        private void OnEnable()
        {
            EventManager<PlayerEvent>.Subscribe(OnPlayerEvent);
        }
        
        private void Start()
        {
            InitializeController();
        }
        
        private void Update()
        {
            HandleInput();
            UpdateGrounded();
        }
        
        private void FixedUpdate()
        {
            ApplyMovement();
            ApplyGravity();
        }
        
        private void LateUpdate()
        {
            UpdateAnimator();
        }
        
        private void OnDisable()
        {
            EventManager<PlayerEvent>.Unsubscribe(OnPlayerEvent);
        }
        
        #endregion
        
        #region Private Methods
        
        private void ValidateComponents()
        {
            if (controller == null)
                controller = GetComponent<CharacterController>();
            
            if (animator == null)
                animator = GetComponent<Animator>();
            
            if (controller == null)
                Debug.LogError($"CharacterController missing on {gameObject.name}", this);
        }
        
        private void HandleInput()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            isSprinting = Input.GetKey(KeyCode.LeftShift);
            
            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                Jump();
            }
        }
        
        private void ApplyMovement()
        {
            Vector3 move = new Vector3(
                Input.GetAxisRaw("Horizontal"),
                0f,
                Input.GetAxisRaw("Vertical")
            ).normalized;
            
            float speed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);
            controller.Move(move * speed * Time.fixedDeltaTime);
        }
        
        private void ApplyGravity()
        {
            if (!isGrounded)
            {
                velocity.y += Physics.gravity.y * Time.fixedDeltaTime;
            }
            else
            {
                velocity.y = Mathf.Max(velocity.y, 0f);
            }
            
            controller.Move(velocity * Time.fixedDeltaTime);
        }
        
        private void Jump()
        {
            velocity.y = jumpForce;
            animator.SetTrigger("Jump");
        }
        
        private void UpdateGrounded()
        {
            isGrounded = controller.isGrounded;
        }
        
        private void UpdateAnimator()
        {
            animator.SetFloat("Speed", controller.velocity.magnitude);
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetBool("IsSprinting", isSprinting);
        }
        
        #endregion
        
        #region Public API
        
        public void TakeDamage(float amount)
        {
            // Implementation
        }
        
        public void Heal(float amount)
        {
            // Implementation
        }
        
        #endregion
        
        #region Events
        
        private void OnPlayerEvent(PlayerEvent evt)
        {
            switch (evt.Type)
            {
                case PlayerEventType.Damaged:
                    HandleDamage(evt.Data);
                    break;
                case PlayerEventType.Healed:
                    HandleHeal(evt.Data);
                    break;
            }
        }
        
        #endregion
    }
}
```

## 4. Object Pooling (Best Practice)

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Универсальный пул объектов
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly Queue<T> pool = new();
        private readonly T prefab;
        private readonly Transform parent;
        private readonly int initialSize;
        
        public ObjectPool(T prefab, int initialSize, Transform parent = null)
        {
            this.prefab = prefab;
            this.parent = parent;
            this.initialSize = initialSize;
            
            Initialize();
        }
        
        private void Initialize()
        {
            for (int i = 0; i < initialSize; i++)
            {
                var obj = CreateInstance();
                pool.Enqueue(obj);
            }
        }
        
        private T CreateInstance()
        {
            var obj = Object.Instantiate(prefab, parent);
            obj.gameObject.SetActive(false);
            return obj;
        }
        
        public T Get()
        {
            T instance;
            
            if (pool.Count > 0)
            {
                instance = pool.Dequeue();
            }
            else
            {
                instance = CreateInstance();
            }
            
            instance.gameObject.SetActive(true);
            return instance;
        }
        
        public void Return(T instance)
        {
            instance.gameObject.SetActive(false);
            pool.Enqueue(instance);
        }
        
        public int Count => pool.Count;
    }
}
```

## 5. ScriptableObject Architecture

```csharp
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Конфигурация оружия через ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Game/Data/WeaponData")]
    public class WeaponData : ScriptableObject
    {
        [Header("Basic Stats")]
        [SerializeField] private string weaponName;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float fireRate = 0.5f;
        [SerializeField] private float range = 100f;
        
        [Header("Visual")]
        [SerializeField] private GameObject weaponPrefab;
        [SerializeField] private ParticleSystem muzzleFlash;
        
        public string Name => weaponName;
        public float Damage => damage;
        public float FireRate => fireRate;
        public float Range => range;
        public GameObject Prefab => weaponPrefab;
        public ParticleSystem MuzzleFlash => muzzleFlash;
    }
}
```

## 6. Event System

```csharp
using System;
using System.Collections.Generic;

namespace Game.Events
{
    /// <summary>
    /// Система событий без параметров
    /// </summary>
    public static class EventManager
    {
        private static readonly Dictionary<string, List<Action>> events = new();
        
        public static void Subscribe(string eventName, Action handler)
        {
            if (!events.ContainsKey(eventName))
            {
                events[eventName] = new List<Action>();
            }
            
            events[eventName].Add(handler);
        }
        
        public static void Unsubscribe(string eventName, Action handler)
        {
            if (events.ContainsKey(eventName))
            {
                events[eventName].Remove(handler);
            }
        }
        
        public static void Trigger(string eventName)
        {
            if (events.ContainsKey(eventName))
            {
                foreach (var handler in events[eventName])
                {
                    handler?.Invoke();
                }
            }
        }
    }
    
    /// <summary>
    /// Система событий с параметрами
    /// </summary>
    public static class EventManager<T>
    {
        private static readonly Dictionary<string, List<Action<T>>> events = new();
        
        public static void Subscribe(string eventName, Action<T> handler)
        {
            if (!events.ContainsKey(eventName))
            {
                events[eventName] = new List<Action<T>>();
            }
            
            events[eventName].Add(handler);
        }
        
        public static void Unsubscribe(string eventName, Action<T> handler)
        {
            if (events.ContainsKey(eventName))
            {
                events[eventName].Remove(handler);
            }
        }
        
        public static void Trigger(string eventName, T data)
        {
            if (events.ContainsKey(eventName))
            {
                foreach (var handler in events[eventName])
                {
                    handler?.Invoke(data);
                }
            }
        }
    }
}
```

## 7. Async/Await в Unity 6

```csharp
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Core
{
    public class AsyncLoader : MonoBehaviour
    {
        private async void Start()
        {
            await LoadGameAsync();
        }
        
        private async Task LoadGameAsync()
        {
            // Показываем экран загрузки
            ShowLoadingScreen();
            
            // Загружаем сцену асинхронно
            var asyncOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("MainScene");
            
            while (!asyncOp.isDone)
            {
                float progress = Mathf.Clamp01(asyncOp.progress / 0.9f);
                UpdateLoadingBar(progress);
                await Task.Yield(); // Возвращаем контроль в основной поток
            }
            
            // Скрываем экран загрузки
            HideLoadingScreen();
        }
        
        private void ShowLoadingScreen() { }
        private void HideLoadingScreen() { }
        private void UpdateLoadingBar(float progress) { }
    }
}
```

## 8. Оптимизация производительности

### Избегаем в Update:
```csharp
// ❌ ПЛОХО
void Update()
{
    GameObject.Find("Player"); // Каждый кадр!
    GetComponent<Rigidbody>(); // Каждый кадр!
    string.Concat("a", "b"); // Создание строк
}

// ✅ ХОРОШО
void Awake()
{
    player = GameObject.Find("Player");
    rigidbody = GetComponent<Rigidbody>();
}

void Update()
{
    // Используем кэшированные ссылки
}
```

### Корректное использование Time.deltaTime:
```csharp
// Физика - используем fixedDeltaTime
void FixedUpdate()
{
    rigidbody.AddForce(Vector3.up * force * Time.fixedDeltaTime);
}

// Всё остальное - deltaTime
void Update()
{
    transform.Translate(Vector3.forward * speed * Time.deltaTime);
}
```

## 9. Unity 6 Specific Features

### Sentis AI Integration:
```csharp
using Unity.Sentis;

public class AIBehavior : MonoBehaviour
{
    private Model runtimeModel;
    private IWorker worker;
    
    private async void Start()
    {
        // Загрузка AI модели через Sentis
        runtimeModel = ModelLoader.Load("Assets/ai_model.onnx");
        worker = WorkerFactory.CreateWorker(BackendType.GPUCompute, runtimeModel);
    }
    
    public async Task<float[]> Predict(float[] input)
    {
        using var inputTensor = new TensorFloat(new TensorShape(input.Length), input);
        worker.Execute(inputTensor);
        
        using var outputTensor = worker.PeekOutput() as TensorFloat;
        return outputTensor.ToReadOnlyArray();
    }
}
```

### UI Toolkit (вместо старого UI):
```csharp
using UnityEngine.UIElements;

public class GameUI : MonoBehaviour
{
    private UIDocument document;
    private Label healthLabel;
    private ProgressBar healthBar;
    
    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        
        // Получаем элементы из UXML
        var root = document.rootVisualElement;
        healthLabel = root.Q<Label>("HealthLabel");
        healthBar = root.Q<ProgressBar>("HealthBar");
    }
    
    public void UpdateHealth(float current, float max)
    {
        healthBar.value = (current / max) * 100f;
        healthLabel.text = $"{current}/{max}";
    }
}
```

## 10. Naming Conventions

- **PascalCase**: Классы, методы, свойства, события
  ```csharp
  public class PlayerController { }
  public void TakeDamage(float amount) { }
  public event EventHandler<PlayerDamagedEventArgs> PlayerDamaged;
  ```

- **camelCase**: Поля, локальные переменные, параметры
  ```csharp
  private float moveSpeed;
  private Rigidbody rb;
  public void Move(float speed) { }
  ```

- **_camelCase**: Приватные поля (опционально)
  ```csharp
  private float _moveSpeed;
  private Rigidbody _rb;
  ```

- **UPPER_SNAKE_CASE**: Константы
  ```csharp
  public const int MAX_HEALTH = 100;
  private const float GRAVITY = -9.81f;
  ```

## 11. Common Patterns

### Singleton (когда действительно нужен):
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

### State Machine:
```csharp
public interface IState
{
    void Enter();
    void Update();
    void Exit();
}

public class StateMachine
{
    private IState currentState;
    
    public void ChangeState(IState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }
    
    public void Update()
    {
        currentState?.Update();
    }
}
```

---

Этот гайд охватывает основные best practices для Unity 6 разработки.
