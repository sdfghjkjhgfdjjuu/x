<!DOCTYPE html>
<html>
<head>
    <title>XYZ C2 - Admin Panel</title>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css" rel="stylesheet">
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.7.2/font/bootstrap-icons.css">
    <style>
        :root {
            --primary-color: #6f42c1;
            --secondary-color: #007bff;
            --dark-bg: #121826;
            --card-bg: #1e293b;
            --text-color: #e2e8f0;
        }
        body {
            background-color: var(--dark-bg);
            color: var(--text-color);
            font-family: "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
        }
        .card {
            background-color: var(--card-bg);
            border: 1px solid rgba(255, 255, 255, 0.1);
            border-radius: 12px;
        }
        .card-header {
            background-color: rgba(255, 255, 255, 0.05);
            border-bottom: 1px solid rgba(255, 255, 255, 0.1);
        }
    </style>
</head>
<body>
    <nav class="navbar navbar-expand-lg navbar-dark" style="background-color: rgba(30, 41, 59, 0.95); border-bottom: 1px solid rgba(255,255,255,0.1);">
        <div class="container-fluid">
            <a class="navbar-brand" href="#"><i class="bi bi-shield-lock"></i> XYZ C2 Panel</a>
        </div>
    </nav>
    <div class="container-fluid py-4">
        <div class="row">
            <div class="col-12">
                <div class="card">
                    <div class="card-header"><h4 class="mb-0"><i class="bi bi-key"></i> Admin Panel Access</h4></div>
                    <div class="card-body">
                        <form method="GET" action="{{ route('admin.dashboard') }}">
                            <div class="mb-3">
                                <label for="password" class="form-label">Enter Admin Password</label>
                                <div class="input-group">
                                    <span class="input-group-text"><i class="bi bi-lock"></i></span>
                                    <input type="password" class="form-control" name="password" id="password" placeholder="Enter admin password" required>
                                </div>
                            </div>
                            <button type="submit" class="btn btn-primary"><i class="bi bi-box-arrow-in-right"></i> Access Admin Panel</button>
                        </form>
                        @if ($error ?? false)
                        <div class="alert alert-danger mt-3 mb-0"><i class="bi bi-exclamation-triangle"></i> Incorrect password. Please try again.</div>
                        @endif
                    </div>
                </div>
            </div>
        </div>
    </div>
</body>
</html>
