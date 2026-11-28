import 'ui_preferences.dart';
import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_webrtc/flutter_webrtc.dart';
import 'api.dart';

class NativeMeetingRoom extends StatefulWidget {
  const NativeMeetingRoom({
    super.key,
    required this.api,
    required this.meetingId,
  });
  final Api api;
  final int meetingId;
  @override
  State<NativeMeetingRoom> createState() => _NativeMeetingRoomState();
}

class _Peer {
  _Peer(this.connection, this.renderer, this.name);
  final RTCPeerConnection connection;
  final RTCVideoRenderer renderer;
  final String name;
  final List<RTCIceCandidate> pending = [];
  bool ready = false;
}

class _NativeMeetingRoomState extends State<NativeMeetingRoom> {
  final local = RTCVideoRenderer();
  final peers = <String, _Peer>{};
  MediaStream? stream, display;
  WebSocket? socket;
  Timer? heartbeat;
  bool mic = true,
      camera = true,
      busy = true,
      owner = false,
      board = false,
      ended = false;
  bool initialized = false, closing = false, rendererDisposed = false;
  Future<void>? releasing;
  String? error, focused;
  String title = 'Meeting room';
  List<dynamic> ice = [];
  final lines = <List<int>>[];
  int generation = 0;
  Offset? point;
  @override
  void initState() {
    super.initState();
    connect();
  }

  void update() {
    if (mounted) setState(() {});
  }

  void send(Map<String, dynamic> value) {
    if (socket?.readyState == WebSocket.open) socket!.add(jsonEncode(value));
  }

  Future<void> connect() async {
    await release();
    if (!mounted) return;
    closing = false;
    mic = true;
    camera = true;
    focused = null;
    error = null;
    busy = true;
    update();
    try {
      if (!initialized) {
        await local.initialize();
        initialized = true;
      }
      final config =
          await widget.api.get(
                '/api/mobile/v2/meetings/${widget.meetingId}/room',
              )
              as Map<String, dynamic>;
      if (!mounted || closing) return;
      title = config['title'];
      owner = config['isOwner'] == true;
      ice = config['iceServers'] as List<dynamic>;
      stream = await navigator.mediaDevices.getUserMedia({
        'audio': true,
        'video': {
          'facingMode': 'user',
          'width': {'ideal': 1280},
          'height': {'ideal': 720},
        },
      });
      if (!mounted || closing) return;
      local.srcObject = stream;
      final url = widget.api.origin
          .resolve('/api/mobile/v2/signal')
          .replace(scheme: widget.api.origin.scheme == 'https' ? 'wss' : 'ws');
      socket = await WebSocket.connect(
        url.toString(),
        headers: {
          HttpHeaders.authorizationHeader: 'Bearer ${widget.api.token}',
        },
      ).timeout(const Duration(seconds: 20));
      send({
        'ticket': config['ticket'],
        'name': widget.api.email ?? 'Employee',
      });
      heartbeat = Timer.periodic(
        const Duration(seconds: 10),
        (_) => send({'type': 'ping'}),
      );
      await for (final raw in socket!) {
        if (closing) break;
        await receive(jsonDecode(raw as String) as Map<String, dynamic>);
      }
      if (!closing && !ended) {
        error ??= 'Connection closed. You can rejoin the meeting.';
      }
    } catch (_) {
      if (!closing) {
        error =
            'Unable to join. Check camera and microphone permissions and your connection, then rejoin.';
      }
    } finally {
      await release();
      busy = false;
      update();
    }
  }

  Future<_Peer> peer(String id, String name) async {
    if (peers[id] != null) return peers[id]!;
    final renderer = RTCVideoRenderer();
    await renderer.initialize();
    final pc = await createPeerConnection({
      'iceServers': ice,
      'sdpSemantics': 'unified-plan',
    });
    final p = _Peer(pc, renderer, name);
    peers[id] = p;
    for (final track in stream!.getTracks()) {
      if (track.kind == 'video' && display != null) {
        await pc.addTrack(display!.getVideoTracks().first, display!);
      } else {
        await pc.addTrack(track, stream!);
      }
    }
    pc.onIceCandidate = (c) {
      if (c.candidate != null) {
        send({'type': 'ice', 'to': id, 'payload': c.toMap()});
      }
    };
    pc.onTrack = (event) {
      if (event.streams.isNotEmpty) {
        renderer.srcObject = event.streams.first;
        update();
      }
    };
    update();
    return p;
  }

  Future<void> receive(Map<String, dynamic> data) async {
    final type = data['type'];
    if (type == 'welcome') {
      busy = false;
      final snapshot = data['board'] as Map<String, dynamic>?;
      generation = snapshot?['generation'] as int? ?? 0;
      lines.clear();
      for (final line in snapshot?['lines'] as List<dynamic>? ?? []) {
        lines.add((line as List).cast<int>());
      }
      for (final item in data['peers'] as List<dynamic>) {
        final p = await peer(item['id'], item['name']);
        final offer = await p.connection.createOffer();
        await p.connection.setLocalDescription(offer);
        send({'type': 'offer', 'to': item['id'], 'payload': offer.toMap()});
      }
    } else if (type == 'offer' || type == 'answer') {
      final id = data['from'] as String;
      final p = await peer(id, data['name'] as String? ?? 'Employee');
      final payload = data['payload'] as Map<String, dynamic>;
      await p.connection.setRemoteDescription(
        RTCSessionDescription(payload['sdp'], payload['type']),
      );
      p.ready = true;
      for (final c in p.pending) {
        await p.connection.addCandidate(c);
      }
      p.pending.clear();
      if (type == 'offer') {
        final answer = await p.connection.createAnswer();
        await p.connection.setLocalDescription(answer);
        send({'type': 'answer', 'to': id, 'payload': answer.toMap()});
      }
    } else if (type == 'ice') {
      final p = await peer(data['from'], data['name'] as String? ?? 'Employee');
      final c = data['payload'];
      final candidate = RTCIceCandidate(
        c['candidate'],
        c['sdpMid'],
        c['sdpMLineIndex'],
      );
      if (p.ready) {
        await p.connection.addCandidate(candidate);
      } else {
        p.pending.add(candidate);
      }
    } else if (type == 'left') {
      final p = peers.remove(data['from']);
      await p?.connection.close();
      await p?.renderer.dispose();
      if (focused == data['from']) focused = null;
    } else if (type == 'mute-all') {
      mic = false;
      for (final t in stream?.getAudioTracks() ?? <MediaStreamTrack>[]) {
        t.enabled = false;
      }
    } else if (type == 'ended') {
      ended = true;
      closing = true;
      await socket?.close();
    } else if (type == 'error') {
      error = data['message'] as String? ?? 'The room is unavailable.';
      closing = true;
      await socket?.close();
    } else if (type == 'board-line' && data['generation'] == generation) {
      lines.add((data['line'] as List).cast<int>());
    } else if (type == 'board-clear') {
      generation = data['generation'];
      lines.clear();
    } else if (type == 'record-start' && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: UiText('The organizer has started recording.')),
      );
    }
    update();
  }

  Future<void> release() =>
      releasing ??= _release().whenComplete(() => releasing = null);

  Future<void> _release() async {
    heartbeat?.cancel();
    heartbeat = null;
    await socket?.close();
    socket = null;
    final old = peers.values.toList();
    peers.clear();
    for (final p in old) {
      await p.connection.close();
      await p.renderer.dispose();
    }
    for (final track in display?.getTracks() ?? <MediaStreamTrack>[]) {
      await track.stop();
    }
    await display?.dispose();
    display = null;
    for (final track in stream?.getTracks() ?? <MediaStreamTrack>[]) {
      await track.stop();
    }
    await stream?.dispose();
    stream = null;
    if (initialized && !rendererDisposed) local.srcObject = null;
  }

  Future<void> share() async {
    try {
      if (display != null) {
        final cameraTrack = stream!.getVideoTracks().first;
        for (final p in peers.values) {
          for (final sender in await p.connection.getSenders()) {
            if (sender.track?.kind == 'video') {
              await sender.replaceTrack(cameraTrack);
            }
          }
        }
        for (final t in display!.getTracks()) {
          await t.stop();
        }
        await display!.dispose();
        display = null;
        local.srcObject = stream;
      } else {
        display = await navigator.mediaDevices.getDisplayMedia({
          'video': true,
          'audio': false,
        });
        final track = display!.getVideoTracks().first;
        for (final p in peers.values) {
          for (final sender in await p.connection.getSenders()) {
            if (sender.track?.kind == 'video') await sender.replaceTrack(track);
          }
        }
        track.onEnded = () {
          if (display != null && mounted) share();
        };
        local.srcObject = display;
      }
      update();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: UiText('Screen sharing could not start on this device.'),
          ),
        );
      }
    }
  }

  @override
  void dispose() {
    closing = true;
    release().then((_) async {
      if (initialized && !rendererDisposed) {
        rendererDisposed = true;
        await local.dispose();
      }
    });
    super.dispose();
  }

  Widget tile(
    String id,
    RTCVideoRenderer video,
    String name, {
    bool mirror = false,
  }) => GestureDetector(
    onTap: () => setState(() => focused = focused == id ? null : id),
    child: Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: const Color(0xff17202e),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Stack(
        children: [
          Positioned.fill(
            child: RTCVideoView(
              video,
              mirror: mirror,
              objectFit: RTCVideoViewObjectFit.RTCVideoViewObjectFitContain,
            ),
          ),
          Positioned(
            bottom: 8,
            left: 8,
            right: 8,
            child: UiText.raw(
              name,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: Colors.white,
                backgroundColor: Colors.black54,
                fontSize: 12,
              ),
            ),
          ),
          const Positioned(
            top: 8,
            right: 8,
            child: Icon(Icons.fullscreen, color: Colors.white70, size: 20),
          ),
        ],
      ),
    ),
  );
  Widget control(
    IconData icon,
    String label,
    VoidCallback? action, {
    Color? color,
  }) => IconButton(
    tooltip: label,
    onPressed: action,
    icon: Icon(icon),
    color: color ?? Colors.white,
  );
  @override
  Widget build(BuildContext context) {
    final active = stream != null && !busy && error == null;
    final tiles = <String, Widget>{
      'self': tile('self', local, 'You', mirror: display == null),
      for (final item in peers.entries)
        item.key: tile(item.key, item.value.renderer, item.value.name),
    };
    return Scaffold(
      backgroundColor: const Color(0xff0c1420),
      appBar: AppBar(
        backgroundColor: const Color(0xff101d30),
        foregroundColor: Colors.white,
        title: UiText.raw(title, style: const TextStyle(fontSize: 16)),
        actions: [
          Padding(
            padding: const EdgeInsets.all(12),
            child: Center(
              child: UiText(
                '${peers.length + 1} people',
                style: const TextStyle(fontSize: 12),
              ),
            ),
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            Expanded(
              child: busy
                  ? const Center(child: CircularProgressIndicator())
                  : error != null || ended || stream == null
                  ? Center(
                      child: Padding(
                        padding: const EdgeInsets.all(24),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            UiText.raw(
                              ended
                                  ? 'This meeting has ended.'
                                  : error ?? 'You left the meeting.',
                              textAlign: TextAlign.center,
                              style: const TextStyle(color: Colors.white),
                            ),
                            const SizedBox(height: 16),
                            if (!ended)
                              FilledButton(
                                onPressed: connect,
                                child: const UiText('Rejoin meeting'),
                              ),
                          ],
                        ),
                      ),
                    )
                  : board
                  ? LayoutBuilder(
                      builder: (_, bounds) => GestureDetector(
                        onPanStart: (d) => point = d.localPosition,
                        onPanUpdate: (d) {
                          final previous = point;
                          point = d.localPosition;
                          if (previous != null) {
                            send({
                              'type': 'board-line',
                              'generation': generation,
                              'line': [
                                previous.dx / bounds.maxWidth * 1000,
                                previous.dy / bounds.maxHeight * 1000,
                                point!.dx / bounds.maxWidth * 1000,
                                point!.dy / bounds.maxHeight * 1000,
                              ].map((n) => n.round().clamp(0, 1000)).toList(),
                            });
                          }
                        },
                        child: ColoredBox(
                          color: Colors.white,
                          child: CustomPaint(
                            size: Size.infinite,
                            painter: _BoardPainter(List.of(lines)),
                          ),
                        ),
                      ),
                    )
                  : Padding(
                      padding: const EdgeInsets.all(10),
                      child: focused != null && tiles[focused] != null
                          ? tiles[focused]!
                          : GridView.count(
                              crossAxisCount: tiles.length == 1 ? 1 : 2,
                              childAspectRatio: 1.25,
                              mainAxisSpacing: 8,
                              crossAxisSpacing: 8,
                              children: tiles.values.toList(),
                            ),
                    ),
            ),
            if (active)
              Wrap(
                alignment: WrapAlignment.center,
                children: [
                  control(
                    mic ? Icons.mic : Icons.mic_off,
                    mic ? 'Mute' : 'Unmute',
                    () {
                      mic = !mic;
                      for (final t in stream!.getAudioTracks()) {
                        t.enabled = mic;
                      }
                      update();
                    },
                  ),
                  control(
                    camera ? Icons.videocam : Icons.videocam_off,
                    'Camera',
                    () {
                      camera = !camera;
                      for (final t in stream!.getVideoTracks()) {
                        t.enabled = camera;
                      }
                      update();
                    },
                  ),
                  control(Icons.cameraswitch, 'Switch camera', () async {
                    final tracks = stream!.getVideoTracks();
                    if (tracks.isNotEmpty) {
                      await Helper.switchCamera(tracks.first);
                    }
                  }),
                  control(
                    Icons.screen_share_outlined,
                    display == null ? 'Share screen' : 'Stop sharing',
                    share,
                  ),
                  control(
                    Icons.draw_outlined,
                    'Whiteboard',
                    () => setState(() => board = !board),
                  ),
                  if (owner && board)
                    control(
                      Icons.delete_outline,
                      'Clear whiteboard',
                      () => send({'type': 'board-clear'}),
                    ),
                  if (owner)
                    control(
                      Icons.volume_off_outlined,
                      'Mute everyone',
                      () => send({'type': 'mute-all'}),
                    ),
                  control(Icons.call_end, 'Leave meeting', () async {
                    closing = true;
                    await release();
                    update();
                  }, color: Colors.redAccent),
                  if (owner)
                    control(
                      Icons.stop_circle_outlined,
                      'End for everyone',
                      () async {
                        final yes = await showDialog<bool>(
                          context: context,
                          builder: (c) => AlertDialog(
                            title: const UiText('End meeting for everyone?'),
                            actions: [
                              TextButton(
                                onPressed: () => Navigator.pop(c, false),
                                child: const UiText('Cancel'),
                              ),
                              FilledButton(
                                onPressed: () => Navigator.pop(c, true),
                                child: const UiText('End meeting'),
                              ),
                            ],
                          ),
                        );
                        if (yes == true) send({'type': 'end-meeting'});
                      },
                      color: Colors.redAccent,
                    ),
                ],
              ),
          ],
        ),
      ),
    );
  }
}

class _BoardPainter extends CustomPainter {
  _BoardPainter(this.lines);
  final List<List<int>> lines;
  @override
  void paint(Canvas canvas, Size size) {
    final pen = Paint()
      ..color = const Color(0xff1745c3)
      ..strokeWidth = 2
      ..strokeCap = StrokeCap.round;
    for (final l in lines) {
      canvas.drawLine(
        Offset(l[0] / 1000 * size.width, l[1] / 1000 * size.height),
        Offset(l[2] / 1000 * size.width, l[3] / 1000 * size.height),
        pen,
      );
    }
  }

  @override
  bool shouldRepaint(covariant _BoardPainter oldDelegate) => true;
}
