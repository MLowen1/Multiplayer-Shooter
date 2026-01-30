using UnityEngine;
using System;
using TMPro;
using PurrNet;

public class MainGameView : View
{
    [SerializeField] private TMP_Text healthText;

    private void Awake()
    {
        InstanceHandler.RegisterInstance(this);
    }

    private void OnDestroy()
    {
        InstanceHandler.UnregisterInstance<MainGameView>();
    }

    public override void OnShow()
    {
        
    }

    public override void OnHide()
    {
        
    }

    public void UpdateHealth(int health)
    {
        healthText.text = health.ToString();
    }
}
