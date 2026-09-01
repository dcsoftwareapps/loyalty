window.kbeautyQrScanner = (() => {
    let stream = null;
    let animationFrame = null;
    let active = false;
    let video = null;
    let canvas = null;
    let context = null;
    let dotNetRef = null;

    async function start(videoElement, dotNetObjectRef, requestedDeviceId = null) {
        stop();

        if (!window.isSecureContext) {
            return {
                Ok: false,
                Message: "Este dispositivo no permite escanear QR desde el navegador. Ingresa el ID del cliente manualmente."
            };
        }

        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            return {
                Ok: false,
                Message: "Este dispositivo no permite escanear QR desde el navegador. Ingresa el ID del cliente manualmente."
            };
        }

        if (typeof window.jsQR !== "function") {
            return {
                Ok: false,
                Message: "El lector QR no esta disponible. Ingresa el ID del cliente manualmente."
            };
        }

        video = videoElement;
        dotNetRef = dotNetObjectRef;
        canvas = document.createElement("canvas");
        context = canvas.getContext("2d", { willReadFrequently: true });

        try {
            stream = await navigator.mediaDevices.getUserMedia({
                audio: false,
                video: requestedDeviceId ? {
                    deviceId: { exact: requestedDeviceId },
                    width: { ideal: 1280 },
                    height: { ideal: 720 }
                } : {
                    facingMode: { ideal: "environment" },
                    width: { ideal: 1280 },
                    height: { ideal: 720 }
                }
            });

            video.srcObject = stream;
            video.setAttribute("playsinline", "true");
            video.muted = true;
            await video.play();

            active = true;
            scanLoop();

            return { Ok: true, Message: null };
        } catch (error) {
            stop();
            return {
                Ok: false,
                Message: "No se pudo acceder a la cámara. Puedes ingresar el ID del cliente manualmente."
            };
        }
    }

    function scanLoop() {
        if (!active || !video || !context || !canvas) {
            return;
        }

        if (video.readyState === video.HAVE_ENOUGH_DATA && video.videoWidth > 0 && video.videoHeight > 0) {
            canvas.width = video.videoWidth;
            canvas.height = video.videoHeight;
            context.drawImage(video, 0, 0, canvas.width, canvas.height);

            const imageData = context.getImageData(0, 0, canvas.width, canvas.height);
            const code = window.jsQR(imageData.data, imageData.width, imageData.height, {
                inversionAttempts: "attemptBoth"
            });

            if (code && code.data) {
                const value = code.data;
                const callback = dotNetRef;
                stop();
                callback?.invokeMethodAsync("OnQrDetected", value);
                return;
            }
        }

        animationFrame = requestAnimationFrame(scanLoop);
    }

    async function switchCamera(videoElement, dotNetObjectRef) {
        if (!navigator.mediaDevices?.enumerateDevices) return { Ok: false, Message: "No hay otra cámara disponible." };
        const devices = (await navigator.mediaDevices.enumerateDevices()).filter(d => d.kind === "videoinput");
        if (devices.length < 2) return { Ok: false, Message: "No hay otra cámara disponible." };
        const currentId = stream?.getVideoTracks()?.[0]?.getSettings()?.deviceId;
        const currentIndex = Math.max(0, devices.findIndex(d => d.deviceId === currentId));
        const next = devices[(currentIndex + 1) % devices.length];
        return start(videoElement, dotNetObjectRef, next.deviceId);
    }

    function stop() {
        active = false;

        if (animationFrame) {
            cancelAnimationFrame(animationFrame);
            animationFrame = null;
        }

        if (video) {
            video.pause();
            video.srcObject = null;
        }

        if (stream) {
            for (const track of stream.getTracks()) {
                track.stop();
            }
            stream = null;
        }

        canvas = null;
        context = null;
        video = null;
        dotNetRef = null;
    }

    return {
        start,
        switchCamera,
        stop
    };
})();
