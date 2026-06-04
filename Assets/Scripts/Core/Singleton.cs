using UnityEngine;

namespace Severance
{
    /// <summary>
    /// MonoBehaviours를 위한 제네릭 싱글톤 기반 클래스입니다.
    /// 스레드 안전한 지연 초기화 및 선택적인 DontDestroyOnLoad 지속성을 지원합니다.
    /// </summary>
    /// <typeparam name="T">구체적인 MonoBehaviour 상속 형식.</typeparam>
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        /// <summary>
        /// 스레드 세이프한 인스턴스 생성을 위한 락 오브젝트.
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// 싱글톤 인스턴스 보관 필드.
        /// </summary>
        private static T _instance;

        /// <summary>
        /// 애플리케이션 종료 여부를 추적하여 고스트 인스턴스가 씬에 생성되는 것을 막습니다.
        /// </summary>
        private static bool _applicationIsQuitting;

        /// <summary>
        /// true일 경우, 해당 싱글톤 GameObject가 새로운 씬 로드 시 파괴되지 않고 유지됩니다.
        /// 파생 클래스에서 오버라이드하여 제어 가능합니다 (기본값: false).
        /// </summary>
        protected virtual bool Persist => false;

        /// <summary>
        /// 싱글톤 인스턴스 프로퍼티입니다. 씬에 존재하지 않을 경우 첫 호출 시점에 탐색 및 자동 생성됩니다.
        /// 게임 종료 과정 중에는 null을 반환합니다.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning(
                        $"[Singleton] 애플리케이션 종료 과정에서 {typeof(T).Name} 인스턴스가 요청되었습니다. null을 반환합니다.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance != null)
                    {
                        return _instance;
                    }

                    // 씬 내에 이미 배치된 동일 유형 오브젝트 탐색
                    T[] instances = FindObjectsByType<T>(FindObjectsSortMode.None);

                    if (instances.Length > 0)
                    {
                        _instance = instances[0];

                        // 중복 생성된 인스턴스가 있다면 파괴
                        for (int i = 1; i < instances.Length; i++)
                        {
                            Debug.LogWarning(
                                $"[Singleton] '{instances[i].gameObject.name}'의 중복된 싱글톤 {typeof(T).Name} 오브젝트를 파괴합니다.");
                            Destroy(instances[i].gameObject);
                        }

                        return _instance;
                    }

                    // 씬 내에 존재하지 않는 경우 새로운 GameObject를 동적으로 생성
                    var singletonObject = new GameObject($"[{typeof(T).Name}]");
                    _instance = singletonObject.AddComponent<T>();
                    Debug.Log($"[Singleton] {typeof(T).Name} 싱글톤 객체를 씬에 자동 생성했습니다.");

                    return _instance;
                }
            }
        }

        /// <summary>
        /// 씬 탐색/생성 과정 없이, 현재 메모리에 할당된 인스턴스가 존재하는지 여부.
        /// </summary>
        public static bool HasInstance => _instance != null;

        /// <summary>
        /// 유니티 Awake 생명주기 콜백. 씬 내 유일성을 강제합니다.
        /// 서브클래스에서 재정의할 때 반드시 <c>base.Awake()</c>를 호출해 주어야 합니다.
        /// </summary>
        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning(
                    $"[Singleton] '{gameObject.name}'의 중복된 {typeof(T).Name} 객체가 발견되어 파괴합니다.");
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;

            if (Persist)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// 싱글톤 컴포넌트가 파괴될 때 정적 참조 인스턴스를 해제합니다.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 애플리케이션 종료 시 플래그를 올려 고스트 생성을 방지합니다.
        /// </summary>
        protected virtual void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }
    }
}
