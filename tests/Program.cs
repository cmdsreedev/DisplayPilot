using DisplayPilot;
int checks=0;
void Check(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
var s=new Settings { Devices = [new() {Name="Desk mouse",Identifier="TEST_MOUSE",Target="PC"},new() {Name="Desk keyboard",Identifier="TEST_KEYBOARD",Target="PC"},new() {Name="TV keyboard",Identifier="TEST_TV_PID|TEST_TV_ADDRESS",Target="TV"}] };int calls=0;var engine=new SwitchEngine(s,_=>{calls++;return Task.CompletedTask;});
Check(engine.Mode=="PC"&&calls==0,"Startup assumes PC without invoking DisplayMagician");
foreach(var id in new[]{"hid#TEST_MOUSE#test","HID#TEST_KEYBOARD"}) {Check(s.Match(id)?.Target=="PC","Desk mapping "+id);await engine.Request(s.Match(id)!.Target);}
Check(calls==0,"First desk events never switch / flicker");
Check(s.Match("BTH#TEST_TV_PID")?.Target=="TV"&&s.Match("bth#test_tv_address")?.Target=="TV","Both TV keyboard identifiers, case insensitive");
await engine.Request("TV");Check(engine.Mode=="TV"&&calls==1,"TV keyboard switches to TV");await engine.Request("PC");Check(calls==1,"Cooldown blocks bounce");await engine.Request("PC",true);Check(calls==2&&engine.Mode=="PC","Manual switch bypasses cooldown");
s.AutomaticSwitching=false;await engine.Request("TV");Check(calls==2,"Paused automatic input ignored");
s.Profiles.Add("Living room cinema");await engine.Request("Living room cinema",true);Check(engine.Mode=="Living room cinema","Arbitrary named profile supported");
await engine.Request("Ignore",true);Check(calls==3,"Ignore never invokes console");
var failed=new SwitchEngine(s,_=>throw new IOException("fake failure"));try{await failed.Request("TV",true);}catch(IOException){}Check(failed.Mode=="PC"&&!failed.Busy,"Failed switch retains current mode and releases lock");
var gate=new TaskCompletionSource();var concurrent=new SwitchEngine(s,_=>gate.Task);var pending=concurrent.Request("TV",true);Check(!await concurrent.Request("Living room cinema",true),"Concurrent switches blocked");gate.SetResult();await pending;
var json=System.Text.Json.JsonSerializer.Serialize(s);var round=System.Text.Json.JsonSerializer.Deserialize<Settings>(json)!;round.Validate();Check(round.Profiles.Count==3&&round.Match("TEST_TV_PID")?.Target=="TV","JSON preserves arbitrary profiles and device mappings");
Console.WriteLine($"{checks} checks passed.");

