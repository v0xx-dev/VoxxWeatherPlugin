using UnityEngine;

namespace VoxxWeatherPlugin.Weathers
{
    public class MeteorWeather: MonoBehaviour
    {
        public GameObject meteorPrefab;

        internal virtual void OnEnable()
        {
            if (meteorPrefab == null)
            {
                Debug.LogError("Meteor prefab is null, disabling meteor weather");
                return;
            }
            //instantiate a copy of the meteor prefab at 0,0,0
            GameObject meteor = Instantiate(meteorPrefab, Vector3.zero, Quaternion.identity);
            //set the meteor to be active
            meteor.SetActive(true);
            //play the meteor's animation
            meteor.GetComponent<Animator>().Play("Flight");
        }

        internal virtual void OnDisable()
        {
            
        }

        internal void OnDestroy()
        {
            
        }

        
    }

    public class MeteorVFXManager: MonoBehaviour
    {
        
        internal void Start()
        {
            
        }

        internal void OnEnable()
        {
        }

        internal void OnDisable()
        {
        }

        internal void FixedUpdate()
        {   
        }

    }
}