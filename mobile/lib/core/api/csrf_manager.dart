class CsrfManager {
  CsrfManager(this._fetchToken);

  final Future<CsrfToken> Function() _fetchToken;

  String? _token;
  String _headerName = 'X-CSRF-TOKEN';
  Future<CsrfToken>? _pending;

  String get headerName => _headerName;
  String? get token => _token;

  void clear() {
    _token = null;
    _pending = null;
  }

  Future<String> ensure() async {
    final cached = _token;
    if (cached != null) {
      return cached;
    }
    final pending = _pending;
    if (pending != null) {
      return (await pending).token;
    }
    final request = _fetchToken();
    _pending = request;
    try {
      final issued = await request;
      _headerName = issued.headerName;
      _token = issued.token;
      return issued.token;
    } finally {
      _pending = null;
    }
  }
}

class CsrfToken {
  const CsrfToken({required this.headerName, required this.token});

  final String headerName;
  final String token;

  factory CsrfToken.fromJson(Map<String, dynamic> json) => CsrfToken(
        headerName: '${json['headerName'] ?? 'X-CSRF-TOKEN'}',
        token: '${json['token'] ?? ''}',
      );
}
