using Godot;
namespace Src.Autoload;

public partial class GameManager : Node
{	
	public float MouseSensitivity {get; set;} = 0.003f;
	public float StickSensitivity {get; set;} = 2.5f;
	public float BaseFov {get; set;} = 75.0f;
	public static GameManager Instance {get; private set;}

    public override void _Ready() {
        Instance = this;
    }
}
