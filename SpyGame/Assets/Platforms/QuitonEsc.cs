using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class QuitOnEsc : MonoBehaviour
{
#if ENABLE_INPUT_SYSTEM
    private InputAction _quitAction;
    private InputAction _resetAction;

    private void OnEnable()
    {
        _quitAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/escape");
        _resetAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/tab");

        _quitAction.performed += OnQuitPerformed;
        _resetAction.performed += OnResetPerformed;

        _quitAction.Enable();
        _resetAction.Enable();
    }

    private void OnDisable()
    {
        if (_quitAction != null)
        {
            _quitAction.performed -= OnQuitPerformed;
            _quitAction.Disable();
            _quitAction.Dispose();
            _quitAction = null;
        }
        if (_resetAction != null)
        {
            _resetAction.performed -= OnResetPerformed;
            _resetAction.Disable();
            _resetAction.Dispose();
            _resetAction = null;
        }
    }

    private void OnQuitPerformed(InputAction.CallbackContext ctx) => Quit();
    private void OnResetPerformed(InputAction.CallbackContext ctx) => ResetLevel();
#else
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Quit();
        if (Input.GetKeyDown(KeyCode.Tab))     ResetLevel();
    }
#endif

    private static void Quit()
    {
        Application.Quit();
    }

    private static void ResetLevel()
    {
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
