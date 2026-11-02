extends SceneTree

func _initialize():
    var node_script = load("res://scripts/generated/LifecycleProbe.cs")
    var instance = node_script.new()
    var observer = node_script.new()
    root.add_child(instance)
    await process_frame
    var valid = instance.get("EnterCount") == 1 and instance.get("ReadyCount") == 1
    root.remove_child(instance)
    valid = valid and instance.get("ExitCount") == 1 and observer.get("DisposedCount") == 0
    root.add_child(instance)
    valid = valid and instance.get("EnterCount") == 2 and instance.get("ReadyCount") == 1
    root.remove_child(instance)
    instance.request_ready()
    root.add_child(instance)
    valid = valid and instance.get("EnterCount") == 3 and instance.get("ReadyCount") == 2
    instance.free()
    valid = valid and observer.get("DisposedCount") == 1 and observer.get("OrderValid")

    var never_entered = node_script.new()
    never_entered.free()
    valid = valid and observer.get("DisposedCount") == 2
    var borrowed = load("res://scripts/generated/BorrowedProbe.cs").new()
    borrowed.free()
    valid = valid and observer.get("DisposedCount") == 2
    observer.free()
    if not valid:
        printerr("Lifecycle, reentry, or disposal contract failed.")
        quit(1)
        return
    print("LIFECYCLE_OK")
    quit(0)
