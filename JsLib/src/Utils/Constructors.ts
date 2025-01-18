import { ObjectLookup } from '../Utils/ObjectLookup';
import { MeshBuilder } from '../Builders/MeshBuilder';

import {
    AnimationMixer,
    Clock,
    Color,
    GridHelper,
    LineBasicMaterial,
    LineSegments,
    Object3D,
    OrthographicCamera,
    PerspectiveCamera,
    Raycaster,
    Scene,
    Vector2,
    Vector3,
    WebGLRenderer,
    Event as ThreeEvent,
    Group,
    BoxGeometry,
    CylinderGeometry,
    MeshBasicMaterial,
    Mesh,
} from 'three';
import { LightBuilder } from '../Builders/LightBuilder';
import { Transforms } from './Transforms';
import { GLTF } from 'three/examples/jsm/loaders/GLTFLoader';
import { Loaders } from '../Viewer/Loaders';


export class FactoryClass {
    
    private makers = new Map<string, Function>();
    private animationMixers: Array<AnimationMixer> = [];

    public constructor() {
        
        this.makers.set('AmbientLight', LightBuilder.BuildAmbientLight);
        this.makers.set('PointLight', LightBuilder.BuildPointLight);
        this.makers.set('Mesh3D', this.establish3DGeometry);
        this.makers.set('Model3D', this.establish3DModel);
        this.makers.set('Text3D', this.establish3DLabel);
        //this.makers.set('Group3D', this.establish3DGroup);
        //this.makers.set('PanelMenu3D', this.establish3DMenu);


    }

    private LoadedObjectComplete(uuid: string) {
        DotNet.invokeMethodAsync('BlazorThreeJS', 'LoadedObjectComplete', uuid);
    }

   public establish3DGeometry(options: any, parent: Object3D): Object3D | null {

        const guid = options.uuid;

        var entity = ObjectLookup.findPrimitive(guid) as Object3D;
        var exist = Boolean(entity)
        if ( !exist ) {
            entity = MeshBuilder.CreateMesh(options);
            ObjectLookup.addPrimitive(guid, entity);
            parent.add(entity);
        }

        MeshBuilder.ApplyMeshTransform(options, entity);
        this.establish3DChildren(options, entity);


        if ( !exist && parent.type === 'Scene' )
        {
            this.LoadedObjectComplete(guid);
            console.log('Geometry Added to Scene', entity);
        }
        return entity;
    }

    private establish3DLabel(options: any, parent: Object3D): Text | null {
        console.log('establish3DLabel modelOptions=', options);

        const guid = options.uuid;

        var entity = ObjectLookup.findLabel(guid);
        var exist = Boolean(entity)
        if ( !exist ) {
            entity = new Text();
            ObjectLookup.addLabel(guid, entity);
            parent.add(entity);

            entity.uuid = guid;
            entity.text = options.text;
            entity.color = options.color;
            entity.fontSize = options.fontSize;
            entity.userData = { isTextLabel: true, };
            //Transforms.setTransform(entity, options.transform);
    
            // Update the rendering:
            entity.sync();
        }

        MeshBuilder.ApplyMeshTransform(options, entity);
        this.establish3DChildren(options, entity);


        if ( !exist && parent.type === 'Scene' )
        {
            this.LoadedObjectComplete(guid);
            console.log('Text Added to Scene', entity);
        }
        return entity;
    }

    private playGltfAnimation(model: GLTF) {
        const animations = model.animations;
        animations?.forEach((animation) => {
            if (Boolean(animation) && Boolean(animation.tracks.length)) {
                const mixer = new AnimationMixer(model.scene);
                this.animationMixers.push(mixer);
                const animationAction = mixer.clipAction(animation);
                animationAction.play();
            }
        });
    }

    public establish3DModel(options: any, parent: Object3D) {
        console.log('establish3DModel modelOptions=', options);
        

        const loaders = new Loaders();
        loaders.import3DModel(options, 
            (model: GLTF) => this.playGltfAnimation(model),
            (item) => {
                ObjectLookup.addModel(item.uuid, item);
                //this.addDebuggerWindow(url, group);
                parent.add(item);
                this.LoadedObjectComplete(item.uuid);
                console.log('Model Added to Scene', item);
            })
    }


    //can we be smart here and call the correct method based on the type of object we are adding?
    public establish3DChildren(options: any, parent: Object3D) {
        
        var members = options.children;
        for (let index = 0; index < members.length; index++) {
            
            //console.log('updateScene element.type=', element.type);
            //console.log('updateScene element=', index, element);
            
            try {
                //add these back in when we have the builders
                //TextPanelBuilder.BuildTextPanels(scene, options);
                //PanelGroupBuilder.BuildPanelGroup(scene, options);
                
                const element = members[index];
                var funct = this.makers.get(element.type);
                if (funct) 
                    funct(element, parent);
                

                // } else
                // if ( element.type == 'Group3D' ) {
                //     this.establish3DGroup(element);
                // } else
                // if ( element.type == 'PanelMenu3D' ) {
                //     this.establish3DMenu(element);
                // } else

                // } else
                // if (element.type.includes('Helper')) {
                //     const obj = this.scene.getObjectByProperty('uuid', element.uuid);
                //     var helper = HelperBuilder.BuildHelper(options, obj);
                //     this.scene.add(helper);
                // }
            } catch (error) {
                console.log('Error in establish3DChildren', error);
            }
        }    
    }

}

export const Constructors = new FactoryClass();
