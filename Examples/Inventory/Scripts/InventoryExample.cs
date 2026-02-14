using ThiccTapeman.Input;
using ThiccTapeman.Inventory;
using UnityEngine;

public class InventoryExample : MonoBehaviour
{
    public InventoryUI inventory1UI;
    public InventoryUI inventory2UI;
    public InventoryUI inventory3UI;
    public InventoryLootSO lootSO;
    public int slotCount = 20;
    public int populateAmount = 10;
    public string repopulateActionName = "InventoryExample.Repopulate";
    public string repopulateBinding = "<Keyboard>/space";

    private Inventory inventory1;
    private Inventory inventory2;
    private Inventory inventory3;
    private InputItem repopulateAction;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var inputManager = InputManager.GetInstance();
        repopulateAction = inputManager.GetTempAction(repopulateActionName, repopulateBinding);

        inventory1 = InventoryManager.GetInstance().CreateInventory(slotCount);
        inventory1.AttachUI(inventory1UI);
        inventory1.PopulateInventory(populateAmount, lootSO);

        inventory2 = InventoryManager.GetInstance().CreateInventory(slotCount);
        inventory2.AttachUI(inventory2UI);

        inventory3 = InventoryManager.GetInstance().CreateInventory(slotCount);
        inventory3.AttachUI(inventory3UI);
    }

    // Update is called once per frame
    void Update()
    {
        if (inventory1 == null || repopulateAction == null) return;
        if (repopulateAction.BufferedTriggered())
        {
            inventory1.Clear();
            inventory1.PopulateInventory(populateAmount, lootSO);
        }
    }
}
