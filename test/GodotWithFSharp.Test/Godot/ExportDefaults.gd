extends SceneTree

func _initialize():
    var node_script = load("res://scripts/generated/Bouncer.cs")
    var instance = node_script.new()
    var valid = node_script.get_property_default_value("Speed") == 180.0
    valid = valid and instance.get("Speed") == 180.0
    instance.set("Speed", 320.0)
    valid = valid and instance.get("Speed") == 320.0
    instance.free()

    var scene = load("res://scenes/Main.tscn").instantiate()
    valid = valid and scene.get_node("Bouncer").get("Speed") == 240.0
    scene.free()
    if not valid:
        printerr("Export defaults or serialized scene values do not match.")
        quit(1)
        return
    print("EXPORT_DEFAULTS_OK")
    quit(0)
