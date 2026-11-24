// Voz en el navegador para Astro Rocket (build WebGL).
//  - Web Speech API (SpeechRecognition): manda a Unity lo que se oye, parcial y final.
//  - getUserMedia + AnalyserNode: nivel de sonido del micrófono (propulsión del cohete).
// Los resultados llegan al GameObject "VoiceCommands" con SendMessage.
mergeInto(LibraryManager.library, {

  VoiceWeb_IsSupported: function () {
    return (window.SpeechRecognition || window.webkitSpeechRecognition) ? 1 : 0;
  },

  VoiceWeb_Start: function (goNamePtr, langPtr) {
    var goName = UTF8ToString(goNamePtr);
    var lang = UTF8ToString(langPtr);
    var V = window.__astroVoice || (window.__astroVoice = { session: 0 });
    V.wanted = true;

    function send(method, value) {
      try {
        if (typeof SendMessage === 'function') SendMessage(goName, method, value);
        else Module.SendMessage(goName, method, value);
      } catch (e) { }
    }

    var SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SR) { send('OnWebVoiceError', 'unsupported'); return; }

    if (!V.rec) {
      var rec = new SR();
      rec.lang = lang;
      rec.continuous = true;
      rec.interimResults = true;   // resultados parciales: el juego reacciona antes
      rec.maxAlternatives = 1;

      rec.onstart = function () { send('OnWebVoiceStarted', ''); };
      rec.onresult = function (e) {
        for (var i = e.resultIndex; i < e.results.length; i++) {
          var r = e.results[i];
          var payload = V.session + ':' + i + '|' + r[0].transcript;
          send(r.isFinal ? 'OnWebVoiceFinal' : 'OnWebVoiceInterim', payload);
        }
      };
      rec.onerror = function (e) {
        if (e.error === 'not-allowed' || e.error === 'service-not-allowed') V.wanted = false;
        send('OnWebVoiceError', e.error);
      };
      // El navegador corta el reconocimiento cada cierto tiempo: se reinicia solo.
      rec.onend = function () {
        if (!V.wanted) return;
        setTimeout(function () {
          if (!V.wanted) return;
          V.session++;
          try { rec.start(); } catch (err) { }
        }, 150);
      };
      V.rec = rec;
    }
    try { V.rec.start(); } catch (err) { }

    // Nivel del micrófono (con cancelación de eco: el sonido del juego molesta menos).
    if (!V.analyser && !V.requestingMic && navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
      V.requestingMic = true;
      navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true } })
        .then(function (stream) {
          var AC = window.AudioContext || window.webkitAudioContext;
          var ctx = new AC();
          var source = ctx.createMediaStreamSource(stream);
          var analyser = ctx.createAnalyser();
          analyser.fftSize = 1024;
          source.connect(analyser);
          V.ctx = ctx;
          V.analyser = analyser;
          V.buffer = new Float32Array(analyser.fftSize);
          // El navegador sólo deja arrancar el audio después de un clic o una tecla.
          var resume = function () { if (ctx.state !== 'running') ctx.resume(); };
          document.addEventListener('pointerdown', resume);
          document.addEventListener('keydown', resume);
          resume();
        })
        .catch(function (err) {
          V.requestingMic = false;
          send('OnWebVoiceError', 'mic-' + (err && err.name ? err.name : 'error'));
        });
    }
  },

  VoiceWeb_Stop: function () {
    var V = window.__astroVoice;
    if (!V) return;
    V.wanted = false;
    if (V.rec) { try { V.rec.stop(); } catch (err) { } }
  },

  VoiceWeb_GetLevel: function () {
    var V = window.__astroVoice;
    if (!V || !V.analyser) return 0;
    V.analyser.getFloatTimeDomainData(V.buffer);
    var sum = 0;
    for (var i = 0; i < V.buffer.length; i++) sum += V.buffer[i] * V.buffer[i];
    return Math.sqrt(sum / V.buffer.length);
  }
});
