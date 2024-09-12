﻿// Decompiled with JetBrains decompiler
// Type: Blazor3D.Viewers.Viewer
// Assembly: Blazor3D, Version=0.1.24.0, Culture=neutral, PublicKeyToken=null
// MVID: 8589B0D0-D62F-4099-9D8A-332F65D16B15
// Assembly location: Blazor3D.dll

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BlazorThreeJS.Cameras;
using BlazorThreeJS.ComponentHelpers;
using BlazorThreeJS.Controls;
using BlazorThreeJS.Core;
using BlazorThreeJS.Events;

using BlazorThreeJS.Lights;
using BlazorThreeJS.Maths;
using BlazorThreeJS.Menus;
using BlazorThreeJS.Objects;
using BlazorThreeJS.Scenes;
using BlazorThreeJS.Settings;
using FoundryRulesAndUnits.Extensions;
using FoundryRulesAndUnits.Units;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace BlazorThreeJS.Viewers
{
    public class SceneDTO
    {
        public Scene? Scene { get; set; }
        public ViewerSettings? ViewerSettings { get; set; }
        public Camera? Camera { get; set; }
        public OrbitControls? OrbitControls { get; set; }
    }

    public class Viewer : ComponentBase, IDisposable
    {
        [Inject] private IJSRuntime? JsRuntime { get; set; }


        //private static event Viewer.SelectedObjectStaticEventHandler ObjectSelectedStatic;

        //private static event Viewer.LoadedObjectStaticEventHandler ObjectLoadedStatic;

        private static Dictionary<Guid, Button> Buttons { get; set; } = new();
        private static Dictionary<Guid, ImportSettings> ImportPromises { get; set; } = new();
        private static Dictionary<Guid, ImportSettings> LoadedModels { get; set; } = new();

        private event LoadedObjectEventHandler? ObjectLoadedPrivate;

        //public event SelectedObjectEventHandler? ObjectSelected;

        public event LoadedObjectEventHandler? ObjectLoaded;

        public event LoadedModuleEventHandler? JsModuleLoaded;
        private JsonSerializerOptions JSONOptions { get; set; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IncludeFields = true,
            IgnoreReadOnlyFields = true
        };

        //private delegate void SelectedObjectStaticEventHandler(Object3DStaticArgs e);
        //private delegate void LoadedObjectStaticEventHandler(Object3DStaticArgs e);

        [Parameter]
        public ViewerSettings ViewerSettings { get; set; }

        [Parameter]
        public Scene ActiveScene { get; set; }

        [Parameter]
        public bool UseDefaultScene { get; set; }

        [Parameter]
        public Camera Camera { get; set; }

        public OrbitControls OrbitControls { get; set; }

        public Viewer()
        {
            // OrthographicCamera camera = new();
            var camera = new PerspectiveCamera()
            {
                Position = new Vector3(3f, 3f, 3f)
            };
            // PerspectiveCamera perspectiveCamera = new PerspectiveCamera();
            // perspectiveCamera.Position = new Vector3(3f, 3f, 3f);
            // ISSUE: reference to a compiler-generated field
            this.Camera = (Camera)camera;
            // ISSUE: reference to a compiler-generated field
            this.OrbitControls = new OrbitControls();
            ActiveScene = new Scene(JsRuntime!);
            this.ViewerSettings = new ViewerSettings();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {

            if (!firstRender)
                return;

            //Viewer.ObjectSelectedStatic += new Viewer.SelectedObjectStaticEventHandler(viewer.OnObjectSelectedStatic);
            //Viewer.ObjectLoadedStatic += new Viewer.LoadedObjectStaticEventHandler(viewer.OnObjectLoadedStatic);
            ObjectLoadedPrivate += new LoadedObjectEventHandler(OnObjectLoadedPrivate);

            LoadedModels.Clear();

            //await JSBridge!.InvokeVoidAsync("import", DotNetObjectReference.Create(this));


            if (UseDefaultScene && !ActiveScene.HasChildren())
                AddDefaultScene();

            var dto = new SceneDTO()
            {
                Scene = ActiveScene,
                ViewerSettings = ViewerSettings,
                Camera = Camera,
                OrbitControls = OrbitControls
            };



            string str = JsonSerializer.Serialize<SceneDTO>(dto, JSONOptions);
            await JsRuntime!.InvokeVoidAsync("BlazorThreeJS.loadViewer", (object)str);
            await ActiveScene.UpdateScene();
            //SRS  I bet we never load a module  so do not do this!!
            //await viewer.OnModuleLoaded();
        }

        private void PopulateButtonsDict()
        {
            Viewer.Buttons.Clear();
            var menus = ActiveScene.GetAllChildren().FindAll((item) => item.Type == "Menu");

            foreach (var menu in menus)
            {
                foreach (var button in ((PanelMenu)menu).Buttons)
                {
                    Console.WriteLine($"From FoundryBlazor Button UUID={button.Uuid}");
                    if (!Viewer.Buttons.ContainsKey(button.Uuid)) Viewer.Buttons.Add(button.Uuid, button);
                }
            }
            //Console.WriteLine($"PopulateButtonsDict menus Count ={menus.Count}");
            //Console.WriteLine($"Viewer.Buttons Count ={Viewer.Buttons.Count}");
        }



        public async Task SetCameraPositionAsync(Vector3 position, Vector3? lookAt = null) => await JsRuntime!.InvokeVoidAsync("BlazorThreeJS.setCameraPosition", (object)position, lookAt);

        public async Task UpdateCamera(Camera camera)
        {
            this.Camera = camera;
            var json = JsonSerializer.Serialize((object)this.Camera, JSONOptions);
            await JsRuntime!.InvokeVoidAsync("BlazorThreeJS.updateCamera", (object)json);
        }

        public async Task ShowCurrentCameraInfo() => await JsRuntime!.InvokeVoidAsync("BlazorThreeJS.showCurrentCameraInfo");

        [JSInvokable]
        public static void ReceiveSelectedObjectUUID(string uuid, Vector3 size)
        {
            Guid guid = string.IsNullOrWhiteSpace(uuid) ? Guid.Empty : Guid.Parse(uuid);
            Console.WriteLine($"ReceiveSelectedObjectUUID size={size.X}, {size.Y}, {size.Z}");

            try
            {
                var item = LoadedModels[guid];
                Console.WriteLine($"item={item}");
                if (item != null)
                {
                    item.ComputedSize = size;
                    item.OnClick.Invoke(item);
                }
                else
                {
                    Console.WriteLine($"uuid={uuid} not found in LoadedModels");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"uuid={uuid} problem (could be a problem in onClick callback). Message={ex.Message}");
            }

        }








        [JSInvokable]
        public static Task OnClickButton(string containerId, string uuid)
        {
            var guid = Guid.Parse(uuid);

            Console.WriteLine($"OnClickButton containerId, uuid={containerId}, {uuid}");
            Console.WriteLine($"After OnClickButton, Viewer.Buttons ContainsKey ={Viewer.Buttons.ContainsKey(guid)}");

            if (Viewer.Buttons.ContainsKey(guid))
            {
                var button = Viewer.Buttons[guid];
                var parms = new List<String>();
                button.OnClick?.Invoke(button);
            }
            return Task.CompletedTask;
        }

        public static Object3D? GetObjectByUuid(Guid uuid, List<Object3D> children) => ChildrenHelper.GetObjectByUuid(uuid, children);

        private async Task OnModuleLoaded()
        {
            if (JsModuleLoaded == null)
                return;

            Delegate[] invocationList = JsModuleLoaded.GetInvocationList();
            Task[] taskArray = new Task[invocationList.Length];

            for (int index = 0; index < invocationList.Length; ++index)
                taskArray[index] = ((LoadedModuleEventHandler)invocationList[index])();

            await Task.WhenAll(taskArray);
        }

        private void AddDefaultScene()
        {
            ActiveScene.Add((Object3D)new AmbientLight());

            PointLight child = new PointLight();
            child.Position = new Vector3()
            {
                X = 1f,
                Y = 3f,
                Z = 0.0f
            };
            ActiveScene.Add((Object3D)child);
            ActiveScene.Add((Object3D)new Mesh());
        }

        private void OnObjectSelectedStatic(Object3DStaticArgs e)
        {
            if (!(this.ViewerSettings.containerId == e.ContainerId))
                return;


            ObjectLoaded?.Invoke(new Object3DArgs() { UUID = e.UUID });
        }

        private void OnObjectLoadedStatic(Object3DStaticArgs e)
        {
            if (!(this.ViewerSettings.containerId == e.ContainerId))
                return;

            this.ObjectLoadedPrivate?.Invoke(new Object3DArgs()
            {
                UUID = e.UUID
            });


            this.ObjectLoaded?.Invoke(new Object3DArgs()
            {
                UUID = e.UUID
            });

        }

        private List<Object3D> ParseChildren(JsonArray? source)
        {
            var children = new List<Object3D>();
            if (source == null)
                return children;

            foreach (var child in source)
            {
                if (child is JsonNode jobject)
                {
                    var name = jobject["name"]?.GetValue<string>() ?? string.Empty;
                    var uuid = jobject["uuid"]?.GetValue<string>() ?? string.Empty;
                    var type = jobject["type"]?.GetValue<string>() ?? string.Empty;
                    var children1 = this.ParseChildren(jobject["children"]?.AsArray());

                    if (type == "Mesh")
                    {
                        var mesh = new Mesh()
                        {
                            Name = name,
                            Uuid = Guid.Parse(type)
                        };
                        children1.Add((Object3D)mesh);
                    }
                    if (type == "Group")
                    {
                        var group = new Group()
                        {
                            Name = name,
                            Uuid = Guid.Parse(type)
                        };

                        group.AddRange(children1);
                    }
                }
            }
            return children;
        }

        private async Task OnObjectLoadedPrivate(Object3DArgs e)
        {
            var options = UnitSpec.JsonHydrateOptions(false);
            string json = await JsRuntime!.InvokeAsync<string>("BlazorThreeJS.getSceneItemByGuid", (object)e.UUID);


            var jobject = JsonNode.Parse(json);
            if (jobject == null)
                return;

            var name = jobject["name"]?.GetValue<string>() ?? string.Empty;
            var uuid = jobject["uuid"]?.GetValue<string>() ?? string.Empty;
            var type = jobject["type"]?.GetValue<string>() ?? string.Empty;
            var children = this.ParseChildren(jobject["children"]?.AsArray());

            if (type.Matches("Group"))
            {
                var group = new Group()
                {
                    Name = name,
                    Uuid = Guid.Parse(uuid),
                };

                group.AddRange(children);
                ActiveScene.Add(group);
            }

            if (type.Matches("Mesh"))
            {

                var mesh = new Mesh()
                {
                    Name = name,
                    Uuid = Guid.Parse(uuid),
                };

                mesh.AddRange(children);
                ActiveScene.Add(mesh);
            }

            this.ObjectLoaded?.Invoke(new Object3DArgs()
            {
                UUID = e.UUID
            });
        }

        public void Dispose()
        {
            //Viewer.ObjectSelectedStatic -= new Viewer.SelectedObjectStaticEventHandler(this.OnObjectSelectedStatic);
            //Viewer.ObjectLoadedStatic -= new Viewer.LoadedObjectStaticEventHandler(this.OnObjectLoadedStatic);
            this.ObjectLoadedPrivate -= new LoadedObjectEventHandler(this.OnObjectLoadedPrivate);
        }

        protected override void BuildRenderTree(RenderTreeBuilder __builder)
        {
            __builder.OpenElement(0, "div");
            __builder.AddAttribute(1, "class", "viewer3dContainer");
            __builder.AddAttribute(2, "id", this.ViewerSettings.containerId);
            __builder.AddAttribute(3, "b-h6holr0slw");
            __builder.CloseElement();
        }



    }
}
