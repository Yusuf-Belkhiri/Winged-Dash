using Aircraft;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.InputSystem;

public class AircraftPlayer : AircraftAgent
{
    //[Header("Input Bindings")] [SerializeField]
    [SerializeField] private InputAction _pitchInput;
    [SerializeField] private InputAction _yawInput;
    [SerializeField] private InputAction _boostInput;
    [SerializeField] private InputAction _pauseInput;


    public override void Initialize()
    {
        base.Initialize();
        // Enable the input          
        _pitchInput.Enable();
        _yawInput.Enable();
        _boostInput.Enable();
        _pauseInput.Enable();
    }

    // Disable the input when destroyed
    private void OnDestroy()
    {
        _pitchInput.Disable();
        _yawInput.Disable();
        _boostInput.Disable();
        _pauseInput.Disable();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
    
        var pitchValue = Mathf.Round(_pitchInput.ReadValue<float>());       
        var yawValue = Mathf.Round(_yawInput.ReadValue<float>());
        var boostValue = Mathf.Round(_boostInput.ReadValue<float>());

        if (pitchValue == -1f) pitchValue = 2f;
        if (yawValue == -1f) yawValue = 2f;

        discreteActions[0] = (int)pitchValue;
        discreteActions[1] = (int)yawValue;
        discreteActions[2] = (int)boostValue;
    }
}
