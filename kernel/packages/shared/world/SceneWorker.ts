import { future } from 'fp-future'
import { ScriptingHost } from 'decentraland-rpc/lib/host'
import { ScriptingTransport } from 'decentraland-rpc/lib/common/json-rpc/types'
import { CustomWebWorkerTransport } from './CustomWebWorkerTransport'

import { playerConfigurations, PREVIEW } from 'config'
import { worldToGrid } from 'atomicHelpers/parcelScenePositions'
import { defaultLogger } from 'shared/logger'
import { EnvironmentAPI } from 'shared/apis/EnvironmentAPI'
import { Vector3, Quaternion, Vector2 } from 'decentraland-ecs/src/decentraland/math'
import { PositionReport, positionObservable } from './positionThings'
import { Observer, Observable } from 'decentraland-ecs/src'
import { sceneLifeCycleObservable } from '../../decentraland-loader/lifecycle/controllers/scene'
import { ParcelSceneAPI } from './ParcelSceneAPI'
import { isRendererEnabled, renderStateObservable } from './worldState'

// tslint:disable-next-line:whitespace
type EngineAPI = import('../apis/EngineAPI').EngineAPI

const gamekitWorkerRaw = require('raw-loader!../../../static/systems/scene.system.js')
const gamekitWorkerBLOB = new Blob([gamekitWorkerRaw])
const gamekitWorkerUrl = URL.createObjectURL(gamekitWorkerBLOB)

const hudWorkerRaw = require('raw-loader!../../../static/systems/decentraland-ui.scene.js')
const hudWorkerBLOB = new Blob([hudWorkerRaw])
export const hudWorkerUrl = URL.createObjectURL(hudWorkerBLOB)

// this function is used in a onSystemReady.then(unmountSystem).
// we keep it separated and global because it is highly reusable
function unmountSystem(system: ScriptingHost) {
  try {
    system.unmount()
  } catch (e) {
    defaultLogger.error('Error unmounting system', e)
  }
}

export class SceneWorker {
  public readonly system = future<ScriptingHost>()

  public engineAPI: EngineAPI | null = null
  public enabled = true

  /** false if this worker part of a dynamically loaded scene */
  public persistent = false
  public readonly onDisposeObservable = new Observable<SceneWorker>()

  public sceneStarted: boolean = false

  public readonly position: Vector3 = new Vector3()
  private readonly lastSentPosition = new Vector3(0, 0, 0)
  private readonly lastSentRotation = new Quaternion(0, 0, 0, 1)
  private positionObserver: Observer<any> | null = null
  private sceneLifeCycleObserver: Observer<any> | null = null
  private renderStateObserver: Observer<any> | null = null

  private sceneReady: boolean = false

  constructor(public parcelScene: ParcelSceneAPI, transport?: ScriptingTransport) {
    parcelScene.registerWorker(this)

    this.subscribeToSceneLifeCycleEvents()
    this.subscribeToWorldRunningEvents()

    this.loadSystem(transport)
      .then(($) => this.system.resolve($))
      .catch(($) => this.system.reject($))
  }

  dispose() {
    if (this.enabled) {
      if (this.positionObserver) {
        positionObservable.remove(this.positionObserver)
        this.positionObserver = null
      }
      if (this.sceneLifeCycleObserver) {
        sceneLifeCycleObservable.remove(this.sceneLifeCycleObserver)
        this.sceneLifeCycleObserver = null
      }
      if (this.renderStateObserver) {
        renderStateObservable.remove(this.renderStateObserver)
        this.renderStateObserver = null
      }

      this.enabled = false

      // Unmount the system
      if (this.system) {
        this.system.then(unmountSystem).catch((e) => defaultLogger.error('Unable to unmount system', e))
      }

      this.parcelScene.dispose()

      this.onDisposeObservable.notifyObservers(this)
    }
  }

  sendUserViewMatrix(positionReport: Readonly<PositionReport>) {
    if (this.engineAPI && 'positionChanged' in this.engineAPI.subscribedEvents) {
      if (!this.lastSentPosition.equals(positionReport.position)) {
        this.engineAPI.sendSubscriptionEvent('positionChanged', {
          position: {
            x: positionReport.position.x - this.position.x,
            z: positionReport.position.z - this.position.z,
            y: positionReport.position.y
          },
          cameraPosition: positionReport.position,
          playerHeight: playerConfigurations.height
        })
        this.lastSentPosition.copyFrom(positionReport.position)
      }
    }

    if (this.engineAPI && 'rotationChanged' in this.engineAPI.subscribedEvents) {
      if (positionReport.quaternion && !this.lastSentRotation.equals(positionReport.quaternion)) {
        this.engineAPI.sendSubscriptionEvent('rotationChanged', {
          rotation: positionReport.rotation,
          quaternion: positionReport.quaternion
        })
        this.lastSentRotation.copyFrom(positionReport.quaternion)
      }
    }
  }

  private subscribeToPositionEvents() {
    const position = Vector2.Zero()

    this.positionObserver = positionObservable.add((obj) => {
      worldToGrid(obj.position, position)

      this.sendUserViewMatrix(obj)
    })
  }

  private subscribeToWorldRunningEvents() {
    this.renderStateObserver = renderStateObservable.add((enabled) => {
      this.sendSceneReadyIfNecessary()
    })
  }

  private subscribeToSceneLifeCycleEvents() {
    this.sceneLifeCycleObserver = sceneLifeCycleObservable.add((obj) => {
      if (this.parcelScene.data.sceneId === obj.sceneId && obj.status === 'ready') {
        this.sceneReady = true
        sceneLifeCycleObservable.remove(this.sceneLifeCycleObserver)
        this.sendSceneReadyIfNecessary()
      }
    })
  }

  private sendSceneReadyIfNecessary() {
    if (!this.sceneStarted && isRendererEnabled() && this.sceneReady) {
      this.sceneStarted = true
      this.engineAPI!.sendSubscriptionEvent('sceneStart', {})
      renderStateObservable.remove(this.renderStateObserver)
    }
  }

  private async startSystem(transport: ScriptingTransport) {
    const system = await ScriptingHost.fromTransport(transport)

    this.engineAPI = system.getAPIInstance('EngineAPI') as EngineAPI
    this.engineAPI.parcelSceneAPI = this.parcelScene

    system.getAPIInstance(EnvironmentAPI).data = this.parcelScene.data

    // TODO: track this errors using rollbar because this kind of event are usually triggered due to setInterval() or unreliable code in scenes, that is not sandboxed
    system.on('error', (e) => {
      // @ts-ignore
      console['log']('Unloading scene because of unhandled exception in the scene worker: ')

      // @ts-ignore
      console['error'](e)

      // These errors should be handled in development time
      if (PREVIEW) {
        debugger
      }

      transport.close()
    })

    system.enable()

    this.subscribeToPositionEvents()

    return system
  }

  private async loadSystem(transport?: ScriptingTransport): Promise<ScriptingHost> {
    if (transport) {
      return this.startSystem(transport)
    } else {
      const worker = new (Worker as any)(gamekitWorkerUrl, {
        name: `ParcelSceneWorker(${this.parcelScene.data.sceneId})`
      })

      // the first error handler will flag the error as a scene worker error enabling error
      // filtering in DCLUnityLoader.js, unhandled errors (like WebSocket messages failing)
      // are not handled by the update loop and therefore those break the whole worker
      const transportOverride = CustomWebWorkerTransport(worker)

      transportOverride.onError!((e: any) => {
        e['isSceneError'] = true
      })

      return this.startSystem(transportOverride)
    }
  }
}
