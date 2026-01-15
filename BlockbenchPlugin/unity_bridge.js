/**
 * Blockbench Plugin: Unity Bridge
 * Connects Blockbench to Unity for automatic GLTF export
 * Uses TCP socket (like cli_text_receiver.js)
 * 
 * Installation:
 * 1. In Blockbench, go to File > Plugins > Load Plugin from File
 * 2. Select this unity_bridge.js file
 */

(() => {
    'use strict';
    
    const PLUGIN_ID = 'unity_bridge';
    const PLUGIN_VERSION = '1.2.0';
    const HOST = '127.0.0.1';
    const DEFAULT_PORT = 29405;
    
    /** @type {import('net').Server | null} */
    let server = null;
    let port = DEFAULT_PORT;
    
    /** @type {Action | null} */
    let actionStart = null;
    /** @type {Action | null} */
    let actionStop = null;
    /** @type {Action | null} */
    let actionSettings = null;
    /** @type {Action | null} */
    let actionForceExport = null;
    /** @type {Action | null} */
    let actionInfo = null;
    
    /** @type {Dialog | null} */
    let settingsDialog = null;
    
    /**
     * Normalize data for writing (handle ArrayBuffer, TypedArray, etc.)
     * Copied from fast_exporter.js approach
     */
    function normalizeDataForWrite(data) {
        let BufferRef = globalThis.Buffer;
        try {
            if (!BufferRef) BufferRef = require('buffer').Buffer;
        } catch (_) {}

        // ArrayBuffer -> Buffer/Uint8Array
        if (typeof ArrayBuffer !== 'undefined' && data instanceof ArrayBuffer) {
            const u8 = new Uint8Array(data);
            return BufferRef ? BufferRef.from(u8) : u8;
        }

        // TypedArray / DataView -> Buffer if available
        if (typeof ArrayBuffer !== 'undefined' && ArrayBuffer.isView && ArrayBuffer.isView(data)) {
            return BufferRef ? BufferRef.from(data) : data;
        }

        return data;
    }

    /**
     * Get effective export options from codec
     */
    function getEffectiveExportOptions(codec) {
        let options = {};
        try {
            if (codec && typeof codec.getExportOptions === 'function') {
                options = codec.getExportOptions() || {};
            }
        } catch (_) {}
        return { ...options };
    }

    /**
     * Export the current model as GLTF to the specified path
     * Uses codec.compile() + codec.write() like fast_exporter.js
     */
    async function exportGLTF(outputPath) {
        // Check if there's an active project
        if (!Project) {
            return { success: false, error: 'No project open' };
        }
        
        try {
            // Use Blockbench's built-in GLTF exporter
            const codec = Codecs.gltf;
            if (!codec) {
                return { success: false, error: 'GLTF codec not available' };
            }
            
            if (typeof codec.compile !== 'function' || typeof codec.write !== 'function') {
                return { success: false, error: 'GLTF codec does not expose compile/write methods' };
            }
            
            const options = getEffectiveExportOptions(codec);
            
            // Compile the GLTF content (may return a Promise)
            let compiled = codec.compile(options);
            if (compiled && typeof compiled.then === 'function') {
                compiled = await compiled;
            }
            
            if (!compiled) {
                return { success: false, error: 'Failed to compile GLTF' };
            }
            
            // Normalize data (handle ArrayBuffer, etc.)
            compiled = normalizeDataForWrite(compiled);
            
            // Use codec's write method (handles multi-file exports, buffers, images, etc.)
            let writeResult = codec.write(compiled, outputPath);
            if (writeResult && typeof writeResult.then === 'function') {
                await writeResult;
            }
            
            // Call afterSave if available
            if (typeof codec.afterSave === 'function') {
                try {
                    codec.afterSave(outputPath);
                } catch (_) {}
            }
            
            console.log('[Unity Bridge] Exported GLTF to:', outputPath);
            return { success: true, path: outputPath };
        } catch (error) {
            const errorMsg = error && error.message ? error.message : String(error);
            console.error('[Unity Bridge] Export error:', errorMsg);
            return { success: false, error: errorMsg };
        }
    }
    
    /**
     * Process a command from Unity
     */
    async function processCommand(commandStr) {
        let command;
        try {
            command = JSON.parse(commandStr);
        } catch (e) {
            return JSON.stringify({ success: false, error: 'Invalid JSON: ' + e.message });
        }
        
        console.log('[Unity Bridge] Processing command:', command.action);
        
        let result;
        
        switch (command.action) {
            case 'ping':
                result = {
                    success: true,
                    status: 'ok',
                    plugin: PLUGIN_ID,
                    version: PLUGIN_VERSION,
                    hasProject: !!Project,
                    projectPath: Project ? Project.save_path : null
                };
                break;
                
            case 'export':
                if (!command.bbmodelPath) {
                    result = { success: false, error: 'bbmodelPath is required' };
                    break;
                }
                
                // Check if there's an active project
                if (!Project || !Project.save_path) {
                    result = { success: false, error: 'No project open' };
                    break;
                }
                
                // Normalize paths for comparison
                const normalizedProjectPath = Project.save_path.replace(/\\/g, '/').toLowerCase();
                const normalizedRequestPath = command.bbmodelPath.replace(/\\/g, '/').toLowerCase();
                
                if (normalizedProjectPath !== normalizedRequestPath) {
                    result = { 
                        success: false, 
                        error: 'Requested model is not currently open. Open: ' + Project.save_path + ', Requested: ' + command.bbmodelPath
                    };
                    break;
                }
                
                // Generate output path (same folder, .gltf extension)
                const outputPath = command.bbmodelPath.replace(/\.bbmodel$/i, '.gltf');
                result = await exportGLTF(outputPath);
                
                if (result.success) {
                    Blockbench.showQuickMessage('Exported GLTF for Unity', 1500);
                }
                break;
                
            case 'force-export':
                if (!Project || !Project.save_path) {
                    result = { success: false, error: 'No project open' };
                    break;
                }
                const forceOutputPath = Project.save_path.replace(/\.bbmodel$/i, '.gltf');
                result = await exportGLTF(forceOutputPath);
                
                if (result.success) {
                    Blockbench.showQuickMessage('Force exported GLTF', 1500);
                }
                break;
                
            default:
                result = { success: false, error: 'Unknown action: ' + command.action };
        }
        
        result.commandId = command.id || null;
        result.timestamp = Date.now();
        
        return JSON.stringify(result);
    }
    
    /**
     * Force export current model as GLTF (for manual trigger / keyboard shortcut)
     */
    async function forceExportCurrentModel() {
        if (!Project || !Project.save_path) {
            Blockbench.showQuickMessage('No project open or project not saved', 2000);
            return;
        }
        
        const outputPath = Project.save_path.replace(/\.bbmodel$/i, '.gltf');
        const result = await exportGLTF(outputPath);
        
        if (result.success) {
            Blockbench.showQuickMessage('Exported to: ' + outputPath, 2000);
        } else {
            Blockbench.showQuickMessage('Export failed: ' + result.error, 3000);
        }
    }
    
    function stopServer(silent = false) {
        if (!server) {
            if (!silent) Blockbench.showQuickMessage('Unity Bridge: not running');
            return;
        }
        try {
            server.close();
        } catch (_) {
            // ignore
        }
        server = null;
        if (!silent) Blockbench.showQuickMessage('Unity Bridge: stopped');
    }
    
    function startServer() {
        if (server) {
            Blockbench.showQuickMessage(`Unity Bridge: already listening on ${HOST}:${port}`);
            return;
        }
        
        let net;
        try {
            net = require('net');
        } catch (err) {
            Blockbench.showMessageBox({
                title: 'Unity Bridge: Desktop app required',
                message: 'This plugin needs the Blockbench desktop app to work.',
                icon: 'error',
            });
            return;
        }
        
        server = net.createServer((socket) => {
            socket.setEncoding('utf8');
            
            let buf = '';
            
            socket.on('data', async (chunk) => {
                buf += chunk;
                
                // Process complete lines
                while (buf.includes('\n')) {
                    const idx = buf.indexOf('\n');
                    const line = buf.slice(0, idx).trim();
                    buf = buf.slice(idx + 1);
                    
                    if (line) {
                        const response = await processCommand(line);
                        socket.write(response + '\n');
                    }
                }
            });
            
            socket.on('end', async () => {
                // Process any remaining data
                if (buf.trim()) {
                    const response = await processCommand(buf.trim());
                    socket.write(response + '\n');
                }
            });
            
            socket.on('error', (e) => {
                console.warn('[Unity Bridge] socket error', e);
            });
        });
        
        server.on('error', (e) => {
            console.error('[Unity Bridge] server error', e);
            const msg = e && e.code === 'EADDRINUSE' 
                ? `Port ${port} is already in use. Try a different port.`
                : String(e && e.message ? e.message : e);
            Blockbench.showMessageBox({
                title: 'Unity Bridge: server error',
                message: msg,
                icon: 'error',
            });
            stopServer(true);
        });
        
        server.listen(port, HOST, () => {
            console.log(`[Unity Bridge] Server listening on ${HOST}:${port}`);
            Blockbench.showQuickMessage(`Unity Bridge: listening on ${HOST}:${port}`);
        });
    }
    
    function ensureSettingsDialog() {
        if (settingsDialog) return settingsDialog;
        
        settingsDialog = new Dialog(`${PLUGIN_ID}_settings`, {
            title: 'Unity Bridge Settings',
            width: 420,
            form: {
                port: {
                    type: 'number',
                    label: 'Port',
                    value: port,
                    min: 1024,
                    max: 65535,
                    step: 1,
                },
            },
            onConfirm(form) {
                const nextPort = Number(form.port);
                if (!Number.isInteger(nextPort) || nextPort < 1024 || nextPort > 65535) {
                    Blockbench.showMessageBox({
                        title: 'Invalid port',
                        message: 'Port must be an integer between 1024 and 65535.',
                        icon: 'error',
                    });
                    return false;
                }
                
                const wasRunning = !!server;
                if (wasRunning) stopServer(true);
                
                port = nextPort;
                
                if (wasRunning) startServer();
                Blockbench.showQuickMessage(`Unity Bridge: port set to ${port}`);
            },
        });
        
        return settingsDialog;
    }
    
    function menuAdd(action) {
        try {
            MenuBar.addAction(action, 'tools');
        } catch (_) {
            try {
                MenuBar.addAction(action, 'file.export');
            } catch (_) {
                // ignore
            }
        }
    }
    
    BBPlugin.register(PLUGIN_ID, {
        title: 'Unity Bridge',
        author: 'Martin Calander',
        description: 'Connects Blockbench to Unity for automatic GLTF export when .bbmodel files are imported.',
        icon: 'sync',
        version: PLUGIN_VERSION,
        min_version: '4.0.0',
        variant: 'desktop',
        
        onload() {
            console.log('[Unity Bridge] Loading plugin...');
            
            actionStart = new Action(`${PLUGIN_ID}_start`, {
                name: 'Unity Bridge: Start Server',
                icon: 'play_arrow',
                click() {
                    startServer();
                },
            });
            
            actionStop = new Action(`${PLUGIN_ID}_stop`, {
                name: 'Unity Bridge: Stop Server',
                icon: 'stop',
                click() {
                    stopServer();
                },
            });
            
            actionSettings = new Action(`${PLUGIN_ID}_settings_action`, {
                name: 'Unity Bridge: Settings',
                icon: 'settings',
                click() {
                    ensureSettingsDialog().show();
                },
            });
            
            actionForceExport = new Action(`${PLUGIN_ID}_force_export`, {
                name: 'Unity Bridge: Force Export GLTF',
                description: 'Force export current model as GLTF for Unity',
                icon: 'file_download',
                keybind: new Keybind({ key: 'b', ctrl: true, shift: true }),
                click: forceExportCurrentModel,
            });
            
            actionInfo = new Action(`${PLUGIN_ID}_info`, {
                name: 'Unity Bridge: Show Info',
                icon: 'info',
                click() {
                    Blockbench.showMessageBox({
                        title: 'Unity Bridge Info',
                        message: `Status: ${server ? 'Running' : 'Stopped'}\n` +
                                 `Address: ${HOST}:${port}\n` +
                                 `Project: ${Project ? Project.save_path : '(none)'}`,
                        icon: 'info',
                        width: 520,
                    });
                },
            });
            
            menuAdd(actionStart);
            menuAdd(actionStop);
            menuAdd(actionSettings);
            menuAdd(actionForceExport);
            menuAdd(actionInfo);
            
            // Auto-start the server
            startServer();
            
            console.log('[Unity Bridge] Plugin loaded');
        },
        
        onunload() {
            console.log('[Unity Bridge] Unloading plugin...');
            stopServer(true);
            
            if (settingsDialog) {
                settingsDialog.delete();
                settingsDialog = null;
            }
            
            for (const a of [actionStart, actionStop, actionSettings, actionForceExport, actionInfo]) {
                try {
                    a && a.delete();
                } catch (_) {
                    // ignore
                }
            }
            
            actionStart = actionStop = actionSettings = actionForceExport = actionInfo = null;
            
            console.log('[Unity Bridge] Plugin unloaded');
        },
    });
})();
