﻿
using BlazorThreeJS.Lights;
using BlazorThreeJS.Maths;

using BlazorThreeJS.Objects;
using BlazorThreeJS.Viewers;
using FoundryRulesAndUnits.Models;
using FoundryRulesAndUnits.Extensions;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using static System.Formats.Asn1.AsnWriter;
using static System.Net.Mime.MediaTypeNames;
using BlazorThreeJS.Settings;



namespace BlazorThreeJS.Core
{
    [JsonDerivedType(typeof(ImportSettings))]
    [JsonDerivedType(typeof(Mesh3D))]
    [JsonDerivedType(typeof(Model3D))]
    [JsonDerivedType(typeof(Group3D))]
    [JsonDerivedType(typeof(TextPanel3D))]
    [JsonDerivedType(typeof(PanelMenu3D))]
    [JsonDerivedType(typeof(Button3D))]
    [JsonDerivedType(typeof(PanelGroup3D))]
    [JsonDerivedType(typeof(AmbientLight))]
    [JsonDerivedType(typeof(PointLight))]
    [JsonDerivedType(typeof(Text3D))]
    [JsonDerivedType(typeof(Scene3D))]
    public abstract class Object3D : ITreeNode
    {
        protected StatusBitArray StatusBits = new();
        public string? Uuid { get; set; }
        //public static int Object3DCount { get; set; } = 0;

        [JsonIgnore]
        public HitBoundary3D? HitBoundary { get; set; }

        [JsonIgnore]
        public Action<Object3D>? OnDelete { get; set; }

        [JsonIgnore]
        public Action<Object3D>? OnBeforeRefresh { get; set; }

        public bool RunAnimation { get; set; } = true;
        protected Action<Object3D, int, double>? OnAnimationUpdate { get; set; } = null;

        public Transform3? Transform { get; set; }

        public string Type { get; } = nameof(Object3D);

        public string Name { get; set; } = string.Empty;


        protected List<Object3D> children = new();

        public IEnumerable<Object3D> Children => children;

        protected Object3D(string type) 
        {
            //Object3DCount++;
            Type = type;
            //Uuid = Object3DCount.ToString();
            StatusBits.IsDirty = true;
        }
        public void SetAnimationUpdate(Action<Object3D, int, double> update)
        {
            OnAnimationUpdate = update;
        }
        public Transform3 GetTransform()
        {
            Transform ??= new Transform3();
            return Transform;
        }

        public virtual bool CollectDirtyObjects(List<Object3D> dirtyObjects, List<Object3D> deletedObjects)
        {

            if (IsDirty)
            {
                dirtyObjects.Add(this);
                IsDirty = false;
                if (OnBeforeRefresh != null)
                    OnBeforeRefresh?.Invoke(this);
            }
            
            var deleteThese = new List<Object3D>();
            foreach (var child in Children)
            {
                if ( child.ShouldDelete())
                    deleteThese.Add(child);

                child.CollectDirtyObjects(dirtyObjects, deletedObjects);
            }

            foreach (var child in deleteThese)
            {
                children.Remove(child);
                deletedObjects.Add(child);
            }

            if (ShouldDelete())
                deletedObjects.Add(this);

            
            return dirtyObjects.Count > 0 || deletedObjects.Count > 0;
        }

        public virtual void UpdateForAnimation(int tick, double fps)
        {
            //send this message to all the children
            foreach (var child in Children)
            {
                try
                {
                    child.UpdateForAnimation(tick, fps);
                }
                catch (System.Exception)
                {
                    $"Error in UpdateForAnimation {child.Name}".WriteError();
                }
            }

            if ( RunAnimation && OnAnimationUpdate != null)
            {
                OnAnimationUpdate?.Invoke(this, tick, fps);
            }
        }

        public void ClearChildren()
        {
            children.Clear();
        }
        public List<Object3D> GetAllChildren()
        {
            return children;
        }

        public virtual string GetTreeNodeTitle()
        {
            return $"{Name} [{Uuid}] => {Type}({GetType().Name})";
        }

        public bool IsDirty
        {
            get { return this.StatusBits.IsDirty; }
            set { 
                this.StatusBits.IsDirty = value; 
                //if ( value )
                //    {
                //        $"Object3D {GetType().Name} {Name} is dirty".WriteNote();
                //    }
                }
        }
        
        public virtual void SetDirty(bool value, bool deep=false)
        {
            if ( IsDirty == value )
                return;

            IsDirty = value;
            if ( deep)
            {
                foreach (var child in Children)
                {
                    child.SetDirty(value, deep);
                }
            }
        }


        public virtual bool ShouldDelete()
        {
            return StatusBits.ShouldDelete;
        }

        public virtual void SetShouldDelete(bool value)
        {
            StatusBits.ShouldDelete = value;
        }

        public async Task ComputeHitBoundary(Scene3D scene, bool deep=true, bool force=false)
        {
            
            if (deep) {
                var list = new List<Task>();
                foreach (var item in Children)
                {
                    list.Add(item.ComputeHitBoundary(scene, true, force));
                }
                await Task.WhenAll(list);
            }
            
            if (HitBoundary != null && !force)
                return;

            //$"Request3DHitBoundary for {Name}  {Uuid}".WriteInfo();

            var boundary = await scene.Request3DHitBoundary(this);

            if ( boundary != null)
            {
                SetExpanded(false);
                HitBoundary = new HitBoundary3D()
                {
                    Uuid = boundary.Uuid,
                    Name = boundary.Name,
                    X = boundary.X,
                    Y = boundary.Y,
                    Z = boundary.Z,
                    Width = boundary.Width,
                    Height = boundary.Height,
                    Depth = boundary.Depth
                };
            }


            
        }

        public virtual IEnumerable<TreeNodeAction> GetTreeNodeActions()
        {
            var result = new List<TreeNodeAction>();

            result.AddAction("Del", "btn-danger", () =>
            {
                Delete();
            });

            if ( OnAnimationUpdate == null)
                return result;

            if (RunAnimation == false)
                result.AddAction("Run", "btn-success", () =>
                {
                    RunAnimation = true;
                });

            if (RunAnimation == true)
                result.AddAction("Stop", "btn-success", () =>
                {
                    RunAnimation = false;
                });
            return result;
        }

        public virtual IEnumerable<ITreeNode> GetTreeChildren()
        {
            var result = new List<ITreeNode>();
            if ( HitBoundary != null)
                result.Add(HitBoundary);

            result.AddRange(GetAllChildren());
            return result;
        }

        public virtual void Delete()
        {
            this.SetShouldDelete(true);
            this.SetDirty(true); //mark this as dirty so that value ill also smash
            //$"Deleting {GetTreeNodeTitle()}".WriteWarning();
            OnDelete?.Invoke(this);
        }

        public virtual (bool success, Object3D result) AddChild(Object3D child)
        {
            if ( child == null)
            {
                //$"AddChild missing  Uuid, {child.Name}".WriteError();  
                return (false, child!);
            }

            var uuid = child.Uuid;
            if ( string.IsNullOrEmpty(uuid))
            {
                //$"AddChild missing  Uuid, {child.Name}".WriteError();  
                return (false, child);
            }

            //what if you have a child with the same uuid? but it is a different object?
            var (found, item) = FindChild(uuid);
            if ( found )
            {
                //$"Object3D AddChild Exist: returning existing {child.Name} -> {item.Name} {item.Uuid}".WriteError();  
                return (false, item!);
            }

            //$"Object3D AddChild {child.Name} -> {this.Name} {this.Uuid}".WriteInfo();
            SetDirty(true);
            children.Add(child);
            child.SetDirty(true);
            return (true, child);
        }


        public virtual (bool success, Object3D result) RemoveChild(Object3D child)
        {
            var found = this.children.Find((item) => item == child);
            if (found == null)
            {
                $"Object3D RemoveChild missing {child.Name} -> {this.Name} {this.Uuid}".WriteError();  
                return (false, child);
            }
            this.children.Remove(child);
            return (true, child);
        }

        public bool HasChildren()
        {
            if ( HitBoundary != null)
                return true;    
            return children.Count > 0;
        }


        public (bool success, Object3D child) FindChild(string uuid)
        {
            var found = this.children.Find((item) => item.Uuid == uuid);
            return (found != null, found!);
        }


        public bool GetIsExpanded()
        {
            return this.StatusBits.IsExpanded;
        }

        public bool SetExpanded(bool value)
        {
            this.StatusBits.IsExpanded = value;
            return value;
        }

        public bool GetIsSelected()
        {
            return this.StatusBits.IsSelected;
        }

        public bool SetSelected(bool value)
        {
            this.StatusBits.IsSelected = value;
            return value;
        }



    }
}
