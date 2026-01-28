using UnityEngine;
using PurrNet;

public class Gun : NetworkBehaviour
{
    protected override void OnSpawned()
    {
        base.OnSpawned();

        enabled = isOwner;
    }

    private void Update()
    {
        
    }
}
