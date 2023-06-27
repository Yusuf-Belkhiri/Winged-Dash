using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Aircraft
{
    public class AircraftArea : MonoBehaviour
    {

    #region FIELDS
        [Tooltip("The path the race will take")] [SerializeField] private CinemachineSmoothPath _racePath;
        [SerializeField] private Transform _checkpointPrefab;
        [SerializeField] private Transform _finishCheckpointPrefab;
        public bool _trainingMode;        // To change the state, if true: enable training mode
            
        public List<AircraftAgent> AircraftAgents { get; private set; }
        public List<Transform> Checkpoints { get; private set; }

    #endregion
        

    #region METHODS
        // Init aircraft agents
        private void Awake()
        {
            AircraftAgents = GetComponentsInChildren<AircraftAgent>().ToList();
            Debug.Assert(AircraftAgents.Count > 0, "No AirCraftAgents found");
        }

        // Init Checkpoint at the cinemachine path units (waypoints)
        private void Start()
        {
            Debug.Assert(_racePath != null, "Race path was not set");
            int numCheckpoints = _racePath.m_Waypoints.Length;
            //int numCheckpoints = (int)_racePath.MaxUnit(CinemachinePathBase.PositionUnits.PathUnits);

            Checkpoints = new List<Transform>(numCheckpoints);
            for (int i = 0; i < numCheckpoints; i++)
            {
                var checkpoint = Instantiate(i == numCheckpoints - 1 ? _finishCheckpointPrefab : _checkpointPrefab, _racePath.transform); // Check for last checkpoint

                // pos & rotation
                checkpoint.localPosition = _racePath.m_Waypoints[i].position;       
                checkpoint.rotation = _racePath.EvaluateOrientationAtUnit(i, CinemachinePathBase.PositionUnits.PathUnits);  // Assign the rotation of the checkpoint as the orientation of the path in the corresponding unit
                
                Checkpoints.Add(checkpoint);
            }
        }

        // Reset the given aircraft agent to its previous checkpoint pos, unless if randomize: pick a random NextCheckpoint => random previousCheckpoint
        public void ResetAgentPosition(AircraftAgent agent, bool randomize = false)
        {
            // Get the previous checkpoint index
            if (randomize)
                agent.NextCheckpointIndex = Random.Range(1, Checkpoints.Count);     // +1 because of the next -1 op
            int previousCheckPointIndex = agent.NextCheckpointIndex - 1;

            // Get the start position & rotation of the previous checkpoint in the world space
            /*
            aircraftAgent.transform.localPosition = Checkpoints[previousCheckPointIndex].position;
            aircraftAgent.transform.rotation = _racePath.EvaluateOrientationAtUnit(previousCheckPointIndex,
                CinemachinePathBase.PositionUnits.PathUnits);   */
            float pathStartPos = 
                _racePath.FromPathNativeUnits(previousCheckPointIndex, CinemachinePathBase.PositionUnits.PathUnits);    // The pos of the previous checkpoint along the race path

            Vector3 worldSpaceStartPos = _racePath.EvaluatePosition(pathStartPos);      // the corresponding world position (convert the pos of the race path to a pos on 3d space)
            Quaternion worldSpaceOrientation = _racePath.EvaluateOrientation(pathStartPos);

            float horizontalPosOffset = (AircraftAgents.IndexOf(agent) - AircraftAgents.Count / 2) * Random.Range(8, 11);      // Random.Range(9, 10) is used to avoid repeating the same position in RL training 

            agent.transform.position =
                worldSpaceStartPos + worldSpaceOrientation * (horizontalPosOffset * Vector3.right);
            agent.transform.rotation = worldSpaceOrientation;
        }
        
    #endregion
    }
}
