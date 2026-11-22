import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class ApiError implements Exception {
  const ApiError(this.message, {this.unauthorized = false});
  final String message;
  final bool unauthorized;
  @override
  String toString() => message;
}

Uri? parseServer(String value, {bool allowLoopback = false}) {
  final uri = Uri.tryParse(value.trim());
  if (uri == null ||
      (uri.scheme != 'https' &&
          !(allowLoopback &&
              uri.scheme == 'http' &&
              uri.host == '127.0.0.1')) ||
      uri.host.isEmpty ||
      uri.userInfo.isNotEmpty ||
      (uri.path.isNotEmpty && uri.path != '/') ||
      uri.hasQuery ||
      uri.hasFragment) {
    return null;
  }
  return uri;
}

class Api {
  Api(this.origin);
  final Uri origin;
  final _client = HttpClient()..connectionTimeout = const Duration(seconds: 20);
  final _storage = const FlutterSecureStorage();
  String? token;
  String? email;
  bool mustChange = false;
  VoidCallback? onUnauthorized;

  Future<bool> restore() async {
    token = await _storage.read(key: 'garowe-native-token');
    if (token == null) return false;
    try {
      await identity();
      return true;
    } on ApiError catch (e) {
      if (e.unauthorized) {
        await clear();
        return false;
      }
      rethrow;
    }
  }

  Future<void> identity() async {
    final data = await get('/api/mobile/v2/auth/me') as Map<String, dynamic>;
    email = data['email'] as String?;
    mustChange = data['mustChangePassword'] == true;
  }

  Future<void> login(String username, String password) async {
    final data =
        await post('/api/mobile/v2/auth/login', {
              'username': username.trim(),
              'password': password,
            })
            as Map<String, dynamic>;
    await _save(data);
    await identity();
  }

  Future<void> changePassword(String current, String password) async {
    final data =
        await post('/api/mobile/v2/auth/password', {
              'current': current,
              'password': password,
            })
            as Map<String, dynamic>;
    await _save(data);
    await identity();
  }

  Future<void> _save(Map<String, dynamic> data) async {
    final value = data['accessToken'];
    if (value is! String || value.isEmpty) {
      throw const ApiError('The server returned an invalid sign-in response.');
    }
    token = value;
    await _storage.write(key: 'garowe-native-token', value: value);
  }

  Future<void> clear() async {
    token = null;
    email = null;
    await _storage.delete(key: 'garowe-native-token');
  }

  Future<void> logout() async {
    await post('/api/mobile/v2/auth/logout', {});
    await clear();
  }

  Future<dynamic> get(String path) => request('GET', path);
  Future<dynamic> post(String path, Map<String, dynamic> body) =>
      request('POST', path, body: body);

  Future<dynamic> request(
    String method,
    String path, {
    Map<String, dynamic>? body,
  }) async {
    try {
      final request = await _client
          .openUrl(method, origin.resolve(path))
          .timeout(const Duration(seconds: 25));
      request.followRedirects = false;
      request.headers.set(HttpHeaders.acceptHeader, 'application/json');
      if (token != null) {
        request.headers.set(HttpHeaders.authorizationHeader, 'Bearer $token');
      }
      if (body != null) {
        request.headers.contentType = ContentType.json;
        request.write(jsonEncode(body));
      }
      final response = await request.close().timeout(
        const Duration(seconds: 25),
      );
      final text = await utf8.decoder
          .bind(response)
          .join()
          .timeout(const Duration(seconds: 25));
      dynamic data;
      try {
        data = text.isEmpty ? null : jsonDecode(text);
      } on FormatException {
        data = null;
      }
      if (response.statusCode == 401 && token != null) {
        await clear();
        onUnauthorized?.call();
        throw const ApiError(
          'Your session expired. Sign in again.',
          unauthorized: true,
        );
      }
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw ApiError(
          data is Map && data['message'] is String
              ? data['message'] as String
              : response.statusCode == 401
              ? 'Your session expired. Sign in again.'
              : response.statusCode == 403
              ? 'Your account does not have access to this service.'
              : response.statusCode == 404
              ? 'This record is unavailable.'
              : 'The server could not complete this request. Try again.',
          unauthorized: response.statusCode == 401 && token != null,
        );
      }
      if (data == null && text.isNotEmpty) {
        throw const ApiError('The server response could not be read.');
      }
      return data;
    } on ApiError {
      rethrow;
    } catch (_) {
      throw const ApiError(
        'Unable to connect. Check your internet connection and try again.',
      );
    }
  }

  Future<Uint8List?> avatar() async {
    final req = await _client
        .getUrl(origin.resolve('/api/mobile/v1/avatar'))
        .timeout(const Duration(seconds: 20));
    req.followRedirects = false;
    req.headers.set(HttpHeaders.authorizationHeader, 'Bearer $token');
    final response = await req.close().timeout(const Duration(seconds: 20));
    if (response.statusCode != 200) {
      await response.drain<void>();
      return null;
    }
    return consolidateHttpClientResponseBytes(response);
  }

  Future<void> uploadPhoto(String path) async {
    final file = File(path);
    if (await file.length() > 10 * 1024 * 1024) {
      throw const ApiError('Choose a photo smaller than 10 MB.');
    }
    final extension = path.split('.').last.toLowerCase();
    if (!['png', 'jpg', 'jpeg'].contains(extension)) {
      throw const ApiError('Choose a JPG or PNG photo.');
    }
    final boundary = 'garowe${DateTime.now().microsecondsSinceEpoch}';
    final request = await _client.postUrl(
      origin.resolve('/api/mobile/v2/profile/photo'),
    );
    request.followRedirects = false;
    request.headers.set(HttpHeaders.authorizationHeader, 'Bearer $token');
    request.headers.set(
      HttpHeaders.contentTypeHeader,
      'multipart/form-data; boundary=$boundary',
    );
    request.add(
      utf8.encode(
        '--$boundary\r\nContent-Disposition: form-data; name="photo"; filename="photo.$extension"\r\nContent-Type: image/${extension == 'png' ? 'png' : 'jpeg'}\r\n\r\n',
      ),
    );
    await request.addStream(file.openRead());
    request.add(utf8.encode('\r\n--$boundary--\r\n'));
    final response = await request.close().timeout(const Duration(seconds: 30));
    final text = await utf8.decoder.bind(response).join();
    if (response.statusCode != 200) {
      dynamic data;
      try {
        data = jsonDecode(text);
      } catch (_) {
        data = null;
      }
      throw ApiError(
        data is Map
            ? data['message']?.toString() ?? 'Photo upload failed.'
            : 'Photo upload failed.',
      );
    }
  }
}
