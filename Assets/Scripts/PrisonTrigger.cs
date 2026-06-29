using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PrisonTrigger : MonoBehaviour
{
    [SerializeField]
    PrisonDodgeballManager.Team Team;

    private void OnTriggerEnter(Collider other)
    {
        var m = other.GetComponent<MinionScript>();

        if(m != null)
        {
            if (m.Team == this.Team)
            {
                m.INTERNAL_TouchingPrison = true;
            }
        }

    }

    private void OnTriggerExit(Collider other)
    {
        var m = other.GetComponent<MinionScript>();

        if (m != null)
        {
            if (m.Team == this.Team)
            {
                m.INTERNAL_TouchingPrison = false;
            }
        }

    }
}
